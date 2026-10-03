using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MmmTool.Interop;

/// <summary>Win32 API の宣言（P/Invoke）</summary>
internal static partial class NativeMethods
{
    #region IME

    /// <summary>フォーカスのあるウィンドウを取得する</summary>
    /// <returns>フォーカスのあるウィンドウのハンドル。無ければ 0</returns>
    [LibraryImport("user32.dll")]
    private static partial nint GetFocus();

    /// <summary>ウィンドウの IME コンテキストを取得する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>IME コンテキストのハンドル。取得できなければ 0</returns>
    [LibraryImport("imm32.dll")]
    private static partial nint ImmGetContext(nint hWnd);

    /// <summary>IME のオン・オフを切り替える</summary>
    /// <param name="hImc">IME コンテキストのハンドル</param>
    /// <param name="open">オンにするなら true</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ImmSetOpenStatus(nint hImc, [MarshalAs(UnmanagedType.Bool)] bool open);

    /// <summary>IME コンテキストを解放する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="hImc">解放する IME コンテキストのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ImmReleaseContext(nint hWnd, nint hImc);

    /// <summary>フォーカスのあるウィンドウの IME をオンにする</summary>
    /// <remarks>日本語入力・漢字変換の状態にする。</remarks>
    public static void TurnOnImeForFocusedWindow() => SetImeForFocusedWindow(true);

    /// <summary>フォーカスのあるウィンドウの IME をオフにする</summary>
    /// <remarks>英数字の直接入力の状態にする。</remarks>
    public static void TurnOffImeForFocusedWindow() => SetImeForFocusedWindow(false);

    /// <summary>フォーカスのあるウィンドウの IME のオン・オフを切り替える</summary>
    /// <param name="open">オンにするなら true、オフなら false</param>
    private static void SetImeForFocusedWindow(bool open)
    {
        var hwnd = GetFocus();
        if (hwnd == 0) return;
        var imc = ImmGetContext(hwnd);
        if (imc == 0) return;
        ImmSetOpenStatus(imc, open);
        ImmReleaseContext(hwnd, imc);
    }

    #endregion

    #region MessageBox

    /// <summary>[OK] ボタンだけを表示する</summary>
    private const uint MB_OK = 0x0;
    /// <summary>情報アイコンを表示する</summary>
    private const uint MB_ICONINFORMATION = 0x40;

    /// <summary>メッセージボックスを表示する</summary>
    /// <param name="hWnd">親ウィンドウのハンドル（無ければ 0）</param>
    /// <param name="text">本文</param>
    /// <param name="caption">タイトル</param>
    /// <param name="type">ボタン・アイコンの種類（<c>MB_*</c>）</param>
    /// <returns>押されたボタンの ID</returns>
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(nint hWnd, string text, string caption, uint type);

    /// <summary>情報のメッセージボックスを表示する</summary>
    /// <param name="text">本文</param>
    /// <param name="caption">タイトル</param>
    public static void ShowInformation(string text, string caption)
        => MessageBox(0, text, caption, MB_OK | MB_ICONINFORMATION);

    #endregion

    #region ウィンドウ

    /// <summary>何もしないメッセージ</summary>
    public const uint WM_NULL = 0x0000;
    /// <summary>ウィンドウが破棄されるメッセージ</summary>
    public const uint WM_DESTROY = 0x0002;
    /// <summary>システム設定が変更されたメッセージ</summary>
    public const uint WM_SETTINGCHANGE = 0x001A;
    /// <summary>オーナードロー項目の描画を求めるメッセージ</summary>
    public const uint WM_DRAWITEM = 0x002B;
    /// <summary>オーナードロー項目の大きさを求めるメッセージ</summary>
    public const uint WM_MEASUREITEM = 0x002C;
    /// <summary>コンテキストメニューを求めるメッセージ</summary>
    public const uint WM_CONTEXTMENU = 0x007B;
    /// <summary>アプリ独自のメッセージ番号の開始値</summary>
    public const uint WM_APP = 0x8000;

    /// <summary>ウィンドウクラスの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct WNDCLASSEXW
    {
        /// <summary>構造体のサイズ（バイト）</summary>
        public uint cbSize;
        /// <summary>クラスのスタイル</summary>
        public uint style;
        /// <summary>ウィンドウプロシージャ</summary>
        public delegate* unmanaged<nint, uint, nint, nint, nint> lpfnWndProc;
        /// <summary>クラスに追加で確保するバイト数</summary>
        public int cbClsExtra;
        /// <summary>ウィンドウに追加で確保するバイト数</summary>
        public int cbWndExtra;
        /// <summary>インスタンスのハンドル</summary>
        public nint hInstance;
        /// <summary>アイコンのハンドル</summary>
        public nint hIcon;
        /// <summary>カーソルのハンドル</summary>
        public nint hCursor;
        /// <summary>背景のブラシ</summary>
        public nint hbrBackground;
        /// <summary>メニューの名前</summary>
        public char* lpszMenuName;
        /// <summary>クラス名</summary>
        public char* lpszClassName;
        /// <summary>小アイコンのハンドル</summary>
        public nint hIconSm;
    }

    /// <summary>モジュールのハンドルを取得する</summary>
    /// <param name="lpModuleName">モジュール名。null なら実行中の EXE</param>
    /// <returns>モジュールのハンドル</returns>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string? lpModuleName);

    /// <summary>ウィンドウクラスを登録する</summary>
    /// <param name="lpwcx">登録するウィンドウクラスの情報</param>
    /// <returns>登録したクラスのアトム。失敗すれば 0</returns>
    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static unsafe partial ushort RegisterClassEx(WNDCLASSEXW* lpwcx);

    /// <summary>ウィンドウクラスの登録を解除する</summary>
    /// <param name="lpClassName">クラス名</param>
    /// <param name="hInstance">クラスを登録したインスタンスのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterClass(string lpClassName, nint hInstance);

    /// <summary>ウィンドウを作成する</summary>
    /// <param name="dwExStyle">拡張ウィンドウスタイル</param>
    /// <param name="lpClassName">クラス名</param>
    /// <param name="lpWindowName">ウィンドウ名</param>
    /// <param name="dwStyle">ウィンドウスタイル</param>
    /// <param name="x">左端の位置</param>
    /// <param name="y">上端の位置</param>
    /// <param name="nWidth">幅</param>
    /// <param name="nHeight">高さ</param>
    /// <param name="hWndParent">親ウィンドウのハンドル</param>
    /// <param name="hMenu">メニューのハンドル</param>
    /// <param name="hInstance">インスタンスのハンドル</param>
    /// <param name="lpParam">作成時に渡す値</param>
    /// <returns>作成したウィンドウのハンドル。失敗すれば 0</returns>
    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    /// <summary>ウィンドウを破棄する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hWnd);

    /// <summary>既定のウィンドウプロシージャ</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="msg">メッセージ</param>
    /// <param name="wParam">メッセージの付加情報（wParam）</param>
    /// <param name="lParam">メッセージの付加情報（lParam）</param>
    /// <returns>メッセージの処理結果</returns>
    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    /// <summary>メッセージをキューに入れる</summary>
    /// <param name="hWnd">送り先ウィンドウのハンドル</param>
    /// <param name="msg">メッセージ</param>
    /// <param name="wParam">メッセージの付加情報（wParam）</param>
    /// <param name="lParam">メッセージの付加情報（lParam）</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    /// <summary>システム全体で一意のメッセージ番号を登録する</summary>
    /// <param name="lpString">メッセージの名前</param>
    /// <returns>登録したメッセージ番号</returns>
    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string lpString);

    /// <summary>ウィンドウを前面に出す</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>前面に出せれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    /// <summary>オーナーウィンドウを表す <see cref="SetWindowLongPtr"/> の番号</summary>
    private const int GWLP_HWNDPARENT = -8;

    /// <summary>ウィンドウの属性を設定する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="nIndex">設定する値のインデックス（<c>GWLP_*</c>）</param>
    /// <param name="dwNewLong">新しい値</param>
    /// <returns>設定前の値</returns>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    /// <summary>ウィンドウのオーナー（持ち主のウィンドウ）を設定する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="owner">オーナーにするウィンドウのハンドル</param>
    /// <remarks>オーナーより常に手前に表示され、オーナーと一緒に最小化される。</remarks>
    public static void SetOwner(nint hWnd, nint owner) => SetWindowLongPtr(hWnd, GWLP_HWNDPARENT, owner);

    /// <summary>ウィンドウのマウス・キーボード入力を有効・無効にする</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="bEnable">有効にするなら true、無効にするなら false</param>
    /// <returns>直前に無効だったなら true</returns>
    /// <remarks>モーダル表示の間、親ウィンドウを操作できないようにするために使う。</remarks>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnableWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    /// <summary>ウィンドウの DPI（ウィンドウがあるモニターの DPI）</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>DPI（100% で 96）</returns>
    /// <remarks>表示する前に大きさを決めるために使う（XamlRoot は表示するまで無いため）。96 が 100%。</remarks>
    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hWnd);

    #endregion

    #region タスクトレイ

    /// <summary>アイコンを追加する</summary>
    public const uint NIM_ADD = 0x0;
    /// <summary>アイコンを変更する</summary>
    public const uint NIM_MODIFY = 0x1;
    /// <summary>アイコンを削除する</summary>
    public const uint NIM_DELETE = 0x2;
    /// <summary>通知のバージョンを設定する</summary>
    public const uint NIM_SETVERSION = 0x4;
    /// <summary>コールバックメッセージを有効にする</summary>
    public const uint NIF_MESSAGE = 0x01;
    /// <summary>アイコンを有効にする</summary>
    public const uint NIF_ICON = 0x02;
    /// <summary>ツールチップを有効にする</summary>
    public const uint NIF_TIP = 0x04;
    /// <summary>バルーン通知を有効にする</summary>
    public const uint NIF_INFO = 0x10;
    /// <summary>標準のツールチップを表示する</summary>
    public const uint NIF_SHOWTIP = 0x80;
    /// <summary>情報アイコン</summary>
    public const uint NIIF_INFO = 0x1;
    /// <summary>エラーアイコン</summary>
    public const uint NIIF_ERROR = 0x3;
    /// <summary>通知のバージョン 4</summary>
    public const uint NOTIFYICON_VERSION_4 = 4;
    /// <summary>アイコンが選択された（クリック）通知</summary>
    public const uint NIN_SELECT = 0x0400;
    /// <summary>アイコンがキーボードで選択された通知</summary>
    public const uint NIN_KEYSELECT = 0x0401;

    /// <summary>トレイアイコンの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NOTIFYICONDATAW
    {
        /// <summary>構造体のサイズ（バイト）</summary>
        public uint cbSize;
        /// <summary>ウィンドウのハンドル</summary>
        public nint hWnd;
        /// <summary>アイコンの識別番号</summary>
        public uint uID;
        /// <summary>有効にする項目のフラグ</summary>
        public uint uFlags;
        /// <summary>通知を受けるメッセージ番号</summary>
        public uint uCallbackMessage;
        /// <summary>アイコンのハンドル</summary>
        public nint hIcon;
        /// <summary>ツールチップの文字</summary>
        public fixed char szTip[128];
        /// <summary>アイコンの状態</summary>
        public uint dwState;
        /// <summary>有効にする状態のビット</summary>
        public uint dwStateMask;
        /// <summary>バルーン通知の本文</summary>
        public fixed char szInfo[256];
        /// <summary>バージョン</summary>
        /// <remarks>uTimeout と共用（NIM_SETVERSION のときはバージョン）。</remarks>
        public uint uVersion;
        /// <summary>バルーン通知のタイトル</summary>
        public fixed char szInfoTitle[64];
        /// <summary>バルーン通知のアイコンの種類</summary>
        public uint dwInfoFlags;
        /// <summary>アイコンの GUID</summary>
        public Guid guidItem;
        /// <summary>バルーン通知のアイコンのハンドル</summary>
        public nint hBalloonIcon;
    }

    /// <summary>トレイアイコンを追加・変更・削除する</summary>
    /// <param name="dwMessage">操作の種類（<c>NIM_*</c>）</param>
    /// <param name="lpData">トレイアイコンの情報</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool Shell_NotifyIcon(uint dwMessage, NOTIFYICONDATAW* lpData);

    /// <summary>固定長の文字列欄へ書き込む</summary>
    /// <param name="value">書き込む文字列</param>
    /// <param name="buffer">書き込み先の固定長バッファ</param>
    /// <param name="length">バッファの文字数（終端を含む）</param>
    /// <remarks>収まらない分は切り捨て、必ず終端する。</remarks>
    public static unsafe void CopyToFixed(string value, char* buffer, int length)
    {
        var count = Math.Min(value.Length, length - 1);
        value.AsSpan(0, count).CopyTo(new Span<char>(buffer, count));
        buffer[count] = '\0';
    }

    #endregion

    #region アイコン

    /// <summary>アイコンの画像</summary>
    public const uint IMAGE_ICON = 1;
    /// <summary>ファイルから読み込む</summary>
    public const uint LR_LOADFROMFILE = 0x10;
    /// <summary>小アイコンの幅（システムメトリック）</summary>
    public const int SM_CXSMICON = 49;
    /// <summary>標準のアプリケーションアイコン</summary>
    public const nint IDI_APPLICATION = 32512;

    /// <summary>画像（アイコン等）を読み込む</summary>
    /// <param name="hInst">リソースを持つインスタンスのハンドル（ファイルから読むときは 0）</param>
    /// <param name="name">ファイルのパスまたはリソース名</param>
    /// <param name="type">画像の種類（<c>IMAGE_*</c>）</param>
    /// <param name="cx">幅</param>
    /// <param name="cy">高さ</param>
    /// <param name="fuLoad">読み込み方法（<c>LR_*</c>）</param>
    /// <returns>画像のハンドル。失敗すれば 0</returns>
    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadImage(nint hInst, string name, uint type, int cx, int cy, uint fuLoad);

    /// <summary>アイコンを読み込む</summary>
    /// <param name="hInstance">リソースを持つインスタンスのハンドル（標準のアイコンなら 0）</param>
    /// <param name="lpIconName">アイコンの名前または標準アイコンの番号</param>
    /// <returns>アイコンのハンドル</returns>
    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    public static partial nint LoadIcon(nint hInstance, nint lpIconName);

    /// <summary>アイコンを破棄する</summary>
    /// <param name="hIcon">アイコンのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint hIcon);

    /// <summary>DPI を指定してシステムメトリックを取得する</summary>
    /// <param name="nIndex">取得する項目（<c>SM_*</c>）</param>
    /// <param name="dpi">基準にする DPI</param>
    /// <returns>取得した値</returns>
    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);

    /// <summary>システムの DPI を取得する</summary>
    /// <returns>システムの DPI（100% で 96）</returns>
    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForSystem();

    #endregion

    #region メニュー

    /// <summary>文字の項目</summary>
    public const uint MF_STRING = 0x0000;
    /// <summary>無効（灰色）の項目</summary>
    public const uint MF_GRAYED = 0x0001;
    /// <summary>サブメニューを開く項目</summary>
    public const uint MF_POPUP = 0x0010;
    /// <summary>オーナードローの項目</summary>
    public const uint MF_OWNERDRAW = 0x0100;
    /// <summary>区切り線</summary>
    public const uint MF_SEPARATOR = 0x0800;
    /// <summary>メニューの右端を指定位置に合わせる</summary>
    public const uint TPM_RIGHTALIGN = 0x0008;
    /// <summary>メニューの下端を指定位置に合わせる</summary>
    public const uint TPM_BOTTOMALIGN = 0x0020;
    /// <summary>右ボタンでも選べる</summary>
    public const uint TPM_RIGHTBUTTON = 0x0002;
    /// <summary>メニュー選択の通知を送らない</summary>
    public const uint TPM_NONOTIFY = 0x0080;
    /// <summary>選ばれた項目の ID を戻り値で返す</summary>
    public const uint TPM_RETURNCMD = 0x0100;
    /// <summary>メニューの開く向き（システムメトリック）</summary>
    public const int SM_MENUDROPALIGNMENT = 40;

    /// <summary>ポップアップメニューを作成する</summary>
    /// <returns>メニューのハンドル。失敗すれば 0</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint CreatePopupMenu();

    /// <summary>オーナードローの項目を追加する</summary>
    /// <param name="hMenu">追加先のメニューのハンドル</param>
    /// <param name="uFlags">項目の種類を示すフラグ（<c>MF_*</c>）</param>
    /// <param name="uIDNewItem">コマンド ID、またはサブメニューのハンドル</param>
    /// <param name="itemData">描画時に受け取る値</param>
    /// <returns>成功すれば true</returns>
    /// <remarks>lpNewItem の代わりに、描画時に受け取る値を渡す。</remarks>
    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendOwnerDrawMenu(nint hMenu, uint uFlags, nuint uIDNewItem, nint itemData);

    /// <summary>メニューを破棄する</summary>
    /// <param name="hMenu">メニューのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyMenu(nint hMenu);

    /// <summary>ポップアップメニューを表示する</summary>
    /// <param name="hMenu">表示するメニューのハンドル</param>
    /// <param name="uFlags">表示方法のフラグ（<c>TPM_*</c>）</param>
    /// <param name="x">表示位置の X（画面座標）</param>
    /// <param name="y">表示位置の Y（画面座標）</param>
    /// <param name="hwnd">メッセージを受けるウィンドウのハンドル</param>
    /// <param name="lptpm">除外領域の情報（使わなければ 0）</param>
    /// <returns>選ばれた項目の ID。選ばれなければ 0</returns>
    [LibraryImport("user32.dll")]
    public static partial int TrackPopupMenuEx(nint hMenu, uint uFlags, int x, int y, nint hwnd, nint lptpm);

    /// <summary>システムメトリックを取得する</summary>
    /// <param name="nIndex">取得する項目（<c>SM_*</c>）</param>
    /// <returns>取得した値</returns>
    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int nIndex);

    #endregion

    #region メニューのダークモード

    // Win32 のメニューは既定では常にライトの見た目になる。uxtheme の非公開 API（序数指定）でシステムのダーク設定に従わせる。
    // Windows 10 1903 以降で使える（エクスプローラー等も使っている）。見つからない環境では何もしない（ライトのまま）

    /// <summary>uxtheme の SetPreferredAppMode の序数</summary>
    private const int UxThemeOrdinalSetPreferredAppMode = 135;
    /// <summary>uxtheme の FlushMenuThemes の序数</summary>
    private const int UxThemeOrdinalFlushMenuThemes = 136;
    /// <summary>システムのダーク設定に従う（AllowDark）</summary>
    private const int PreferredAppModeAllowDark = 1;

    /// <summary>DLL を読み込む</summary>
    /// <param name="lpLibFileName">DLL のファイル名</param>
    /// <returns>モジュールのハンドル。失敗すれば 0</returns>
    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibrary(string lpLibFileName);

    /// <summary>DLL の関数のアドレスを取得する</summary>
    /// <param name="hModule">モジュールのハンドル</param>
    /// <param name="lpProcName">関数の序数（下位ワードに指定）</param>
    /// <returns>関数のアドレス。見つからなければ 0</returns>
    [LibraryImport("kernel32.dll")]
    private static partial nint GetProcAddress(nint hModule, nint lpProcName);

    /// <summary>メニューをシステムのダーク／ライト設定に従わせる。</summary>
    public static unsafe void AllowDarkMenus()
    {
        var uxtheme = LoadLibrary("uxtheme.dll");
        if (uxtheme == 0) return;

        var setPreferredAppMode = (delegate* unmanaged<int, int>)GetProcAddress(uxtheme, UxThemeOrdinalSetPreferredAppMode);
        if (setPreferredAppMode is not null)
        {
            setPreferredAppMode(PreferredAppModeAllowDark);
        }
        FlushMenuThemes();
    }

    /// <summary>メニューの見た目をいまのテーマで作り直させる</summary>
    /// <remarks>ダーク／ライトの切り替え時に使う。</remarks>
    public static unsafe void FlushMenuThemes()
    {
        var uxtheme = LoadLibrary("uxtheme.dll");
        if (uxtheme == 0) return;

        var flushMenuThemes = (delegate* unmanaged<void>)GetProcAddress(uxtheme, UxThemeOrdinalFlushMenuThemes);
        if (flushMenuThemes is not null)
        {
            flushMenuThemes();
        }
    }

    #endregion

    #region メニューのオーナードロー（GDI）

    /// <summary>メニュー項目のオーナードロー</summary>
    public const uint ODT_MENU = 1;
    /// <summary>項目が選択されている</summary>
    public const uint ODS_SELECTED = 0x0001;
    /// <summary>項目が無効（灰色）</summary>
    public const uint ODS_GRAYED = 0x0002;
    /// <summary>項目が無効</summary>
    public const uint ODS_DISABLED = 0x0004;
    /// <summary>背景のブラシを設定する</summary>
    public const uint MIM_BACKGROUND = 0x00000002;
    /// <summary>スタイルを設定する</summary>
    public const uint MIM_STYLE = 0x00000010;
    /// <summary>サブメニューにも適用する</summary>
    public const uint MIM_APPLYTOSUBMENUS = 0x80000000;
    /// <summary>チェックマーク用の左の余白を無くす</summary>
    public const uint MNS_NOCHECK = 0x80000000;
    /// <summary>水平方向の中央に揃える</summary>
    public const uint DT_CENTER = 0x0001;
    /// <summary>垂直方向の中央に揃える</summary>
    public const uint DT_VCENTER = 0x0004;
    /// <summary>1 行で描く</summary>
    public const uint DT_SINGLELINE = 0x0020;
    /// <summary>描かずに大きさだけ計算する</summary>
    public const uint DT_CALCRECT = 0x0400;
    /// <summary>& を下線の指定として扱わない</summary>
    public const uint DT_NOPREFIX = 0x0800;
    /// <summary>収まらない末尾を省略記号にする</summary>
    public const uint DT_END_ELLIPSIS = 0x8000;
    /// <summary>背景を塗りつぶさない</summary>
    public const int TRANSPARENT = 1;
    /// <summary>何も描かないペン</summary>
    public const int NULL_PEN = 8;
    /// <summary>既定の文字セット</summary>
    public const byte DEFAULT_CHARSET = 1;
    /// <summary>ClearType で描く品質</summary>
    public const byte CLEARTYPE_QUALITY = 5;
    /// <summary>最も近いモニターを返す</summary>
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    /// <summary>実効 DPI</summary>
    public const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>四角形</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        /// <summary>左端</summary>
        public int left;
        /// <summary>上端</summary>
        public int top;
        /// <summary>右端</summary>
        public int right;
        /// <summary>下端</summary>
        public int bottom;
    }

    /// <summary>座標</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        /// <summary>X 座標</summary>
        public int x;
        /// <summary>Y 座標</summary>
        public int y;
    }

    /// <summary>オーナードロー項目の大きさを求める情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MEASUREITEMSTRUCT
    {
        /// <summary>コントロールの種類</summary>
        public uint CtlType;
        /// <summary>コントロールの ID</summary>
        public uint CtlID;
        /// <summary>項目の ID</summary>
        public uint itemID;
        /// <summary>項目の幅</summary>
        public uint itemWidth;
        /// <summary>項目の高さ</summary>
        public uint itemHeight;
        /// <summary>項目に持たせた値</summary>
        public nuint itemData;
    }

    /// <summary>オーナードロー項目を描く情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DRAWITEMSTRUCT
    {
        /// <summary>コントロールの種類</summary>
        public uint CtlType;
        /// <summary>コントロールの ID</summary>
        public uint CtlID;
        /// <summary>項目の ID</summary>
        public uint itemID;
        /// <summary>描画の動作</summary>
        public uint itemAction;
        /// <summary>項目の状態</summary>
        public uint itemState;
        /// <summary>コントロールまたはメニューのハンドル</summary>
        public nint hwndItem;
        /// <summary>描画先のデバイスコンテキスト</summary>
        public nint hDC;
        /// <summary>項目の四角形</summary>
        public RECT rcItem;
        /// <summary>項目に持たせた値</summary>
        public nuint itemData;
    }

    /// <summary>メニューの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MENUINFO
    {
        /// <summary>構造体のサイズ（バイト）</summary>
        public uint cbSize;
        /// <summary>有効にする項目のフラグ</summary>
        public uint fMask;
        /// <summary>メニューのスタイル</summary>
        public uint dwStyle;
        /// <summary>メニューの最大の高さ</summary>
        public uint cyMax;
        /// <summary>背景のブラシ</summary>
        public nint hbrBack;
        /// <summary>コンテキストヘルプの ID</summary>
        public uint dwContextHelpID;
        /// <summary>メニューに持たせた値</summary>
        public nuint dwMenuData;
    }

    /// <summary>論理フォントの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LOGFONTW
    {
        /// <summary>文字の高さ</summary>
        public int lfHeight;
        /// <summary>文字の平均の幅</summary>
        public int lfWidth;
        /// <summary>行の角度</summary>
        public int lfEscapement;
        /// <summary>文字の角度</summary>
        public int lfOrientation;
        /// <summary>太さ</summary>
        public int lfWeight;
        /// <summary>斜体</summary>
        public byte lfItalic;
        /// <summary>下線</summary>
        public byte lfUnderline;
        /// <summary>取り消し線</summary>
        public byte lfStrikeOut;
        /// <summary>文字セット</summary>
        public byte lfCharSet;
        /// <summary>出力の精度</summary>
        public byte lfOutPrecision;
        /// <summary>クリップの精度</summary>
        public byte lfClipPrecision;
        /// <summary>出力の品質</summary>
        public byte lfQuality;
        /// <summary>ピッチとファミリー</summary>
        public byte lfPitchAndFamily;
        /// <summary>フォント名</summary>
        public fixed char lfFaceName[32];
    }

    /// <summary>メニューの情報を設定する</summary>
    /// <param name="hMenu">メニューのハンドル</param>
    /// <param name="lpcmi">設定するメニューの情報</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool SetMenuInfo(nint hMenu, MENUINFO* lpcmi);

    /// <summary>デバイスコンテキストを取得する</summary>
    /// <param name="hWnd">ウィンドウのハンドル（画面全体なら 0）</param>
    /// <returns>デバイスコンテキストのハンドル</returns>
    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hWnd);

    /// <summary>デバイスコンテキストを解放する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="hDC">解放するデバイスコンテキストのハンドル</param>
    /// <returns>解放できれば 1</returns>
    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hWnd, nint hDC);

    /// <summary>四角形を塗りつぶす</summary>
    /// <param name="hDC">デバイスコンテキストのハンドル</param>
    /// <param name="lprc">塗りつぶす四角形</param>
    /// <param name="hbr">ブラシのハンドル</param>
    /// <returns>成功すれば 0 以外</returns>
    [LibraryImport("user32.dll")]
    public static unsafe partial int FillRect(nint hDC, RECT* lprc, nint hbr);

    /// <summary>文字を描く</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="lpchText">描く文字列</param>
    /// <param name="cchText">文字数（-1 で終端まで）</param>
    /// <param name="lprc">描く範囲の四角形</param>
    /// <param name="format">描き方のフラグ（<c>DT_*</c>）</param>
    /// <returns>描いた文字の高さ</returns>
    [LibraryImport("user32.dll", EntryPoint = "DrawTextW", StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial int DrawText(nint hdc, string lpchText, int cchText, RECT* lprc, uint format);

    /// <summary>座標があるモニターを取得する</summary>
    /// <param name="pt">座標（画面座標）</param>
    /// <param name="dwFlags">モニターが無いときの扱い（<c>MONITOR_*</c>）</param>
    /// <returns>モニターのハンドル</returns>
    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT pt, uint dwFlags);

    /// <summary>モニターの DPI を取得する</summary>
    /// <param name="hmonitor">モニターのハンドル</param>
    /// <param name="dpiType">DPI の種類（<c>MDT_*</c>）</param>
    /// <param name="dpiX">水平方向の DPI を受け取る</param>
    /// <param name="dpiY">垂直方向の DPI を受け取る</param>
    /// <returns>成功すれば 0</returns>
    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>論理フォントからフォントを作成する</summary>
    /// <param name="lplf">フォントの情報</param>
    /// <returns>フォントのハンドル</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontIndirectW")]
    public static unsafe partial nint CreateFontIndirect(LOGFONTW* lplf);

    /// <summary>フォントを列挙する</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="lpLogfont">列挙する条件のフォント情報</param>
    /// <param name="lpProc">見つかるたびに呼ぶコールバック</param>
    /// <param name="lParam">コールバックへ渡す値</param>
    /// <param name="dwFlags">予約（0）</param>
    /// <returns>コールバックが最後に返した値</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "EnumFontFamiliesExW")]
    public static unsafe partial int EnumFontFamiliesEx(
        nint hdc, LOGFONTW* lpLogfont, delegate* unmanaged<void*, void*, uint, nint, int> lpProc, nint lParam, uint dwFlags);

    /// <summary>描画オブジェクトを選択する</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="h">選択する描画オブジェクトのハンドル</param>
    /// <returns>直前に選択されていたオブジェクトのハンドル</returns>
    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint h);

    /// <summary>描画オブジェクトを削除する</summary>
    /// <param name="ho">描画オブジェクトのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    /// <summary>標準の描画オブジェクトを取得する</summary>
    /// <param name="i">標準オブジェクトの種類（<c>NULL_PEN</c> など）</param>
    /// <returns>描画オブジェクトのハンドル</returns>
    [LibraryImport("gdi32.dll")]
    public static partial nint GetStockObject(int i);

    /// <summary>単色のブラシを作成する</summary>
    /// <param name="color">色（COLORREF）</param>
    /// <returns>ブラシのハンドル</returns>
    [LibraryImport("gdi32.dll")]
    public static partial nint CreateSolidBrush(uint color);

    /// <summary>文字の色を設定する</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="color">文字の色（COLORREF）</param>
    /// <returns>直前の文字の色</returns>
    [LibraryImport("gdi32.dll")]
    public static partial uint SetTextColor(nint hdc, uint color);

    /// <summary>背景の描き方を設定する</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="mode">背景の描き方（<c>TRANSPARENT</c> など）</param>
    /// <returns>直前の描き方</returns>
    [LibraryImport("gdi32.dll")]
    public static partial int SetBkMode(nint hdc, int mode);

    /// <summary>角丸の四角形を描く</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="left">左端</param>
    /// <param name="top">上端</param>
    /// <param name="right">右端</param>
    /// <param name="bottom">下端</param>
    /// <param name="width">角の丸みの幅</param>
    /// <param name="height">角の丸みの高さ</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RoundRect(nint hdc, int left, int top, int right, int bottom, int width, int height);

    /// <summary>クリップ領域から四角形を除く</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="left">除く四角形の左端</param>
    /// <param name="top">除く四角形の上端</param>
    /// <param name="right">除く四角形の右端</param>
    /// <param name="bottom">除く四角形の下端</param>
    /// <returns>クリップ領域の種類</returns>
    [LibraryImport("gdi32.dll")]
    public static partial int ExcludeClipRect(nint hdc, int left, int top, int right, int bottom);

    #endregion

    #region 擬似コンソール（ConPTY）

    /// <summary>拡張の起動情報（STARTUPINFOEX）を使う</summary>
    public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    /// <summary>標準入出力のハンドルを指定する</summary>
    public const int STARTF_USESTDHANDLES = 0x00000100;
    /// <summary>擬似コンソールを渡す属性</summary>
    public static readonly nint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    /// <summary>端末の大きさ（列・行）</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct COORD
    {
        /// <summary>列数</summary>
        public short X;
        /// <summary>行数</summary>
        public short Y;
    }

    /// <summary>プロセスの起動情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFOW
    {
        /// <summary>構造体のサイズ（バイト）</summary>
        public int cb;
        /// <summary>予約</summary>
        public nint lpReserved;
        /// <summary>デスクトップの名前</summary>
        public nint lpDesktop;
        /// <summary>コンソールのタイトル</summary>
        public nint lpTitle;
        /// <summary>ウィンドウの X 座標</summary>
        public int dwX;
        /// <summary>ウィンドウの Y 座標</summary>
        public int dwY;
        /// <summary>ウィンドウの幅</summary>
        public int dwXSize;
        /// <summary>ウィンドウの高さ</summary>
        public int dwYSize;
        /// <summary>コンソールの列数</summary>
        public int dwXCountChars;
        /// <summary>コンソールの行数</summary>
        public int dwYCountChars;
        /// <summary>コンソールの文字と背景の色</summary>
        public int dwFillAttribute;
        /// <summary>有効にする項目のフラグ</summary>
        public int dwFlags;
        /// <summary>ウィンドウの表示方法</summary>
        public short wShowWindow;
        /// <summary>予約のサイズ</summary>
        public short cbReserved2;
        /// <summary>予約</summary>
        public nint lpReserved2;
        /// <summary>標準入力のハンドル</summary>
        public nint hStdInput;
        /// <summary>標準出力のハンドル</summary>
        public nint hStdOutput;
        /// <summary>標準エラーのハンドル</summary>
        public nint hStdError;
    }

    /// <summary>属性リストを持てる、プロセスの起動情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFOEXW
    {
        /// <summary>基本の起動情報</summary>
        public STARTUPINFOW StartupInfo;
        /// <summary>属性リスト</summary>
        public nint lpAttributeList;
    }

    /// <summary>作成したプロセスの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        /// <summary>プロセスのハンドル</summary>
        public nint hProcess;
        /// <summary>スレッドのハンドル</summary>
        public nint hThread;
        /// <summary>プロセスの ID</summary>
        public int dwProcessId;
        /// <summary>スレッドの ID</summary>
        public int dwThreadId;
    }

    /// <summary>パイプを作成する</summary>
    /// <param name="hReadPipe">読み取り側のハンドルを受け取る</param>
    /// <param name="hWritePipe">書き込み側のハンドルを受け取る</param>
    /// <param name="lpPipeAttributes">セキュリティ属性（使わなければ 0）</param>
    /// <param name="nSize">バッファのサイズ（0 で既定）</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, nint lpPipeAttributes, int nSize);

    /// <summary>擬似コンソールを作成する</summary>
    /// <param name="size">端末の大きさ</param>
    /// <param name="hInput">擬似コンソールが入力を読むハンドル</param>
    /// <param name="hOutput">擬似コンソールが出力を書くハンドル</param>
    /// <param name="dwFlags">作成時のフラグ</param>
    /// <param name="phPC">擬似コンソールのハンドルを受け取る</param>
    /// <returns>結果の HRESULT</returns>
    [LibraryImport("kernel32.dll")]
    public static partial int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out nint phPC);

    /// <summary>擬似コンソールの大きさを変更する</summary>
    /// <param name="hPC">擬似コンソールのハンドル</param>
    /// <param name="size">新しい端末の大きさ</param>
    /// <returns>結果の HRESULT</returns>
    [LibraryImport("kernel32.dll")]
    public static partial int ResizePseudoConsole(nint hPC, COORD size);

    /// <summary>擬似コンソールを閉じる</summary>
    /// <param name="hPC">擬似コンソールのハンドル</param>
    [LibraryImport("kernel32.dll")]
    public static partial void ClosePseudoConsole(nint hPC);

    /// <summary>プロセス属性リストを初期化する</summary>
    /// <param name="lpAttributeList">属性リストのバッファ（サイズを調べるときは 0）</param>
    /// <param name="dwAttributeCount">属性の数</param>
    /// <param name="dwFlags">予約（0）</param>
    /// <param name="lpSize">必要なサイズ（バイト）</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InitializeProcThreadAttributeList(nint lpAttributeList, int dwAttributeCount, int dwFlags, ref nint lpSize);

    /// <summary>プロセス属性リストに属性を設定する</summary>
    /// <param name="lpAttributeList">属性リスト</param>
    /// <param name="dwFlags">予約（0）</param>
    /// <param name="attribute">設定する属性（<c>PROC_THREAD_ATTRIBUTE_*</c>）</param>
    /// <param name="lpValue">属性の値</param>
    /// <param name="cbSize">属性の値のサイズ（バイト）</param>
    /// <param name="lpPreviousValue">予約（0）</param>
    /// <param name="lpReturnSize">予約（0）</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateProcThreadAttribute(nint lpAttributeList, uint dwFlags, nint attribute, nint lpValue, nint cbSize, nint lpPreviousValue, nint lpReturnSize);

    /// <summary>プロセス属性リストを破棄する</summary>
    /// <param name="lpAttributeList">破棄する属性リスト</param>
    [LibraryImport("kernel32.dll")]
    public static partial void DeleteProcThreadAttributeList(nint lpAttributeList);

    /// <summary>プロセスを作成する。</summary>
    /// <param name="lpApplicationName">実行ファイルのパス（コマンドラインで指定するなら null）</param>
    /// <param name="lpCommandLine">コマンドライン（書き込み可能なバッファ）</param>
    /// <param name="lpProcessAttributes">プロセスのセキュリティ属性（使わなければ 0）</param>
    /// <param name="lpThreadAttributes">スレッドのセキュリティ属性（使わなければ 0）</param>
    /// <param name="bInheritHandles">ハンドルを子に引き継ぐか</param>
    /// <param name="dwCreationFlags">作成のフラグ（<c>EXTENDED_STARTUPINFO_PRESENT</c> など）</param>
    /// <param name="lpEnvironment">環境変数のブロック（親と同じなら 0）</param>
    /// <param name="lpCurrentDirectory">作業ディレクトリ。親と同じなら null</param>
    /// <param name="lpStartupInfo">起動情報</param>
    /// <param name="lpProcessInformation">作成したプロセスの情報を受け取る</param>
    /// <returns>成功すれば true</returns>
    /// <remarks>lpCommandLine は API 側で書き換えられることがあるため、書き込み可能なバッファを渡す。</remarks>
    [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool CreateProcess(
        string? lpApplicationName,
        char* lpCommandLine,
        nint lpProcessAttributes,
        nint lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        nint lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEXW lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    /// <summary>ハンドルを閉じる</summary>
    /// <param name="hObject">閉じるハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    #endregion
}
