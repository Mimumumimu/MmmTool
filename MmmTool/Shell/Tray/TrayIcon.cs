using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using static MmmTool.Interop.NativeMethods;

namespace MmmTool.Shell.Tray;

/// <summary>
/// タスクトレイのアイコンと右クリックメニュー。Win32 の Shell_NotifyIcon を直接使う。
/// </summary>
/// <remarks>
/// トレイからの通知を受けるため、専用の非表示ウィンドウを作る（メインウィンドウとは独立）。
/// メッセージ専用ウィンドウ（HWND_MESSAGE）にしないのは、エクスプローラー再起動の通知（TaskbarCreated）がブロードキャストで、
/// メッセージ専用ウィンドウには届かないため。UI スレッドで作り、UI スレッドで破棄する。
/// </remarks>
public sealed class TrayIcon : IDisposable
{
    /// <summary>トレイアイコンの識別番号</summary>
    private const uint IconId = 1;
    /// <summary>ツールチップの文字</summary>
    private const string ToolTip = "MmmTool";

    /// <summary>通知を受けるウィンドウのクラス名。</summary>
    /// <remarks>クラスの登録はプロセスごとなので、別の EXE（Debug / Release 等）と重なっても問題ない。</remarks>
    private const string ClassName = "MmmTool_Tray";

    /// <summary>トレイアイコンの操作（クリック・右クリック等）の通知。</summary>
    private const uint CallbackMessage = WM_APP + 1;

    /// <summary>メッセージを受けるインスタンス</summary>
    /// <remarks>ウィンドウプロシージャは static のため。トレイはアプリに 1 つ。</remarks>
    private static TrayIcon? s_current;

    /// <summary>メニューに項目を出す機能</summary>
    private readonly IEnumerable<ITrayMenuSource> _sources;
    /// <summary>TaskbarCreated（エクスプローラー再起動の通知）のメッセージ番号</summary>
    private readonly uint _taskbarCreatedMessage;
    /// <summary>UI スレッドのディスパッチャー</summary>
    private readonly DispatcherQueue _dispatcher;

    /// <summary>表示中のメニューのコマンド ID と処理。</summary>
    private readonly Dictionary<int, Func<Task>> _commands = [];

    /// <summary>表示中のメニューの描画</summary>
    /// <remarks>メニューを開いている間だけ持つ。</remarks>
    private TrayMenuRenderer? _renderer;

    /// <summary>通知を受けるウィンドウのハンドル</summary>
    private nint _hwnd;

    /// <summary>ファイルから読んだアイコン</summary>
    /// <remarks>自分で破棄する。標準のアイコンは破棄しないので持たない。</remarks>
    private nint _icon;
    /// <summary>破棄済みか</summary>
    private bool _disposed;

    /// <summary>トレイアイコンを作る（表示は <see cref="Show"/> で行う）</summary>
    /// <param name="sources">メニューに項目を出す機能</param>
    public TrayIcon(IEnumerable<ITrayMenuSource> sources)
    {
        _sources = sources;
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>メインウィンドウを出すよう求められた（アイコンのクリック）。</summary>
    public event EventHandler? OpenRequested;

    /// <summary>メニューの「終了」が選ばれた。</summary>
    public event EventHandler? ExitRequested;

    /// <summary>トレイにアイコンを出す。</summary>
    public unsafe void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hwnd != 0) return;

        s_current = this;
        AllowDarkMenus();

        var hInstance = GetModuleHandle(null);
        fixed (char* className = ClassName)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = hInstance,
                lpszClassName = className,
            };
            if (RegisterClassEx(&windowClass) == 0)
            {
                throw new InvalidOperationException($"トレイ用のウィンドウクラスを登録できませんでした（エラー {Marshal.GetLastPInvokeError()}）。");
            }
        }

        _hwnd = CreateWindowEx(0, ClassName, ToolTip, 0, 0, 0, 0, 0, 0, 0, hInstance, 0);
        if (_hwnd == 0)
        {
            throw new InvalidOperationException($"トレイ用のウィンドウを作成できませんでした（エラー {Marshal.GetLastPInvokeError()}）。");
        }

        _icon = LoadTrayIcon();
        AddIcon();
    }

    /// <summary>トレイに出すアイコン</summary>
    /// <remarks>読めなければ Windows 標準のアプリアイコン（トレイから操作できなくなるのを避ける）。</remarks>
    private nint DisplayIcon => _icon != 0 ? _icon : LoadIcon(0, IDI_APPLICATION);

    /// <summary>トレイから通知を出す</summary>
    /// <param name="title">通知のタイトル</param>
    /// <param name="message">通知の本文</param>
    /// <param name="isError">エラーのアイコンで出すか（false なら情報のアイコン）</param>
    /// <remarks>Windows の通知として表示される。</remarks>
    public unsafe void ShowNotification(string title, string message, bool isError)
    {
        if (_hwnd == 0) return;

        var data = CreateData(NIF_INFO);
        CopyToFixed(title, data.szInfoTitle, 64);
        CopyToFixed(message, data.szInfo, 256);
        data.dwInfoFlags = isError ? NIIF_ERROR : NIIF_INFO;
        Shell_NotifyIcon(NIM_MODIFY, &data);
    }

    #region アイコン

    /// <summary>Shell_NotifyIcon に渡すデータを作る</summary>
    /// <param name="flags">有効にする項目を示すフラグ（<c>NIF_*</c>）</param>
    /// <returns>トレイアイコンの識別情報を入れたデータ</returns>
    private unsafe NOTIFYICONDATAW CreateData(uint flags) => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = flags,
    };

    /// <summary>トレイにアイコンを登録する。</summary>
    /// <remarks>エクスプローラーがまだ起動していない等で失敗しても、起動後の TaskbarCreated で登録し直す。</remarks>
    private unsafe void AddIcon()
    {
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = DisplayIcon;
        CopyToFixed(ToolTip, data.szTip, 128);
        if (!Shell_NotifyIcon(NIM_ADD, &data))
        {
            return;
        }

        // 新しい通知の形式（クリック = NIN_SELECT、右クリック = WM_CONTEXTMENU、座標は wParam）を使う
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, &data);
    }

    /// <summary>トレイからアイコンを消す</summary>
    private unsafe void RemoveIcon()
    {
        var data = CreateData(0);
        Shell_NotifyIcon(NIM_DELETE, &data);
    }

    /// <summary>EXE と同じ場所の Assets\app.ico を、トレイの大きさ（DPI に合わせた小アイコン）で読む。</summary>
    /// <returns>アイコンのハンドル。ファイルが無い等で読めなければ 0（呼び出し側で Windows 標準のアイコンに代える）</returns>
    private static nint LoadTrayIcon()
    {
        var size = GetSystemMetricsForDpi(SM_CXSMICON, GetDpiForSystem());
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        return LoadImage(0, path, IMAGE_ICON, size, size, LR_LOADFROMFILE);
    }

    #endregion

    #region メッセージ

    /// <summary>通知を受けるウィンドウのウィンドウプロシージャ</summary>
    /// <param name="hwnd">ウィンドウのハンドル</param>
    /// <param name="msg">メッセージ</param>
    /// <param name="wParam">メッセージの付加情報（wParam）</param>
    /// <param name="lParam">メッセージの付加情報（lParam）</param>
    /// <returns>メッセージの処理結果</returns>
    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
        => s_current?.HandleMessage(hwnd, msg, wParam, lParam) ?? DefWindowProc(hwnd, msg, wParam, lParam);

    /// <summary>メッセージを処理する</summary>
    /// <param name="hwnd">ウィンドウのハンドル</param>
    /// <param name="msg">メッセージ</param>
    /// <param name="wParam">メッセージの付加情報（wParam）</param>
    /// <param name="lParam">メッセージの付加情報（lParam）</param>
    /// <returns>メッセージの処理結果</returns>
    private unsafe nint HandleMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        // エクスプローラーが再起動するとトレイアイコンが消えるので、登録し直す
        if (msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            AddIcon();
            return 0;
        }

        switch (msg)
        {
            case CallbackMessage:
                switch ((uint)(lParam & 0xFFFF))
                {
                    case NIN_SELECT:
                    case NIN_KEYSELECT:
                        // メッセージ処理の中で画面を操作しないよう、処理が戻ってから行う
                        _dispatcher.TryEnqueue(() => OpenRequested?.Invoke(this, EventArgs.Empty));
                        break;
                    case WM_CONTEXTMENU:
                        // 座標は wParam の下位・上位 16 ビット（符号付き。マルチモニターで負になる）
                        ShowMenu((short)(wParam & 0xFFFF), (short)((wParam >> 16) & 0xFFFF));
                        break;
                }
                return 0;

            // メニューの項目は自分で描く（メニューを開いている間だけ届く）
            case WM_MEASUREITEM when _renderer is not null && ((MEASUREITEMSTRUCT*)lParam)->CtlType == ODT_MENU:
                _renderer.Measure((MEASUREITEMSTRUCT*)lParam);
                return 1;

            case WM_DRAWITEM when _renderer is not null && ((DRAWITEMSTRUCT*)lParam)->CtlType == ODT_MENU:
                _renderer.Draw((DRAWITEMSTRUCT*)lParam);
                return 1;

            case WM_SETTINGCHANGE:
                // ダーク／ライトの切り替えをメニューに反映する
                FlushMenuThemes();
                break;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    #endregion

    #region メニュー

    /// <summary>右クリックメニューを出し、選ばれた項目の処理を行う。</summary>
    /// <param name="x">メニューを出す位置の X（画面座標）</param>
    /// <param name="y">メニューを出す位置の Y（画面座標）</param>
    /// <remarks>メニューは開くたびに作る（各機能の最新の内容を出すため）。</remarks>
    private void ShowMenu(int x, int y)
    {
        var menu = CreatePopupMenu();
        if (menu == 0) return;

        var renderer = _renderer = new TrayMenuRenderer(x, y);
        try
        {
            _commands.Clear();
            var nextId = 1;

            foreach (var section in _sources.Select(source => source.GetItems()).Where(items => items.Count > 0))
            {
                AppendItems(menu, section, ref nextId);
                renderer.AppendSeparator(menu);
            }

            AddCommand(menu, "終了", () =>
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            }, ref nextId);
            renderer.ApplyTo(menu);

            // 前面にしておかないと、メニューの外をクリックしても閉じない（Win32 のトレイメニューの決まり）
            SetForegroundWindow(_hwnd);
            var flags = TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY | TPM_BOTTOMALIGN;
            if (GetSystemMetrics(SM_MENUDROPALIGNMENT) != 0)
            {
                flags |= TPM_RIGHTALIGN;
            }
            var selected = TrackPopupMenuEx(menu, flags, x, y, _hwnd, 0);
            PostMessage(_hwnd, WM_NULL, 0, 0);

            if (_commands.TryGetValue(selected, out var command))
            {
                _dispatcher.TryEnqueue(() => _ = InvokeAsync(command));
            }
        }
        finally
        {
            // サブメニューも一緒に破棄される。描画用のブラシ・フォントはメニューが使っているので、メニューのあとに破棄する
            DestroyMenu(menu);
            _renderer = null;
            renderer.Dispose();
        }
    }

    /// <summary>項目をメニューに追加する</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    /// <param name="items">追加する項目</param>
    /// <param name="nextId">次に割り当てるコマンド ID。使った分だけ進む</param>
    private void AppendItems(nint menu, IReadOnlyList<TrayMenuItem> items, ref int nextId)
    {
        var renderer = _renderer!;
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                renderer.AppendSeparator(menu);
            }
            else if (item.Children is { Count: > 0 } children)
            {
                var submenu = CreatePopupMenu();
                AppendItems(submenu, children, ref nextId);
                renderer.AppendSubmenu(menu, submenu, item.Text, item.IsEnabled);
            }
            else if (item is { IsEnabled: true, Invoked: { } invoked })
            {
                AddCommand(menu, item.Text, invoked, ref nextId);
            }
            else
            {
                renderer.AppendCommand(menu, 0, item.Text, isEnabled: false);
            }
        }
    }

    /// <summary>コマンドの項目をメニューに追加する</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    /// <param name="text">表示する文字</param>
    /// <param name="invoked">選ばれたときの処理</param>
    /// <param name="nextId">次に割り当てるコマンド ID。使った分だけ進む</param>
    private void AddCommand(nint menu, string text, Func<Task> invoked, ref int nextId)
    {
        var id = nextId++;
        _commands[id] = invoked;
        _renderer!.AppendCommand(menu, id, text, isEnabled: true);
    }

    /// <summary>項目の処理を行う</summary>
    /// <param name="command">項目の処理</param>
    /// <returns>処理の完了を表すタスク</returns>
    /// <remarks>失敗したらトレイの通知で知らせる（ウィンドウが隠れていても気付けるように）。</remarks>
    private async Task InvokeAsync(Func<Task> command)
    {
        try
        {
            await command();
        }
        catch (Exception ex)
        {
            ShowNotification("MmmTool", ex.Message, isError: true);
        }
    }

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != 0)
        {
            RemoveIcon();
            DestroyWindow(_hwnd);
            UnregisterClass(ClassName, GetModuleHandle(null));
            _hwnd = 0;
        }
        if (_icon != 0)
        {
            DestroyIcon(_icon);
            _icon = 0;
        }
        s_current = null;
    }
}
