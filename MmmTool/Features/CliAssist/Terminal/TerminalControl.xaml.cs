using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using MmmSdk.Core.Tasks;

namespace MmmTool.Features.CliAssist.Terminal;

/// <summary>
/// WebView2 上の xterm.js で端末を描画し、<see cref="ITerminalSession"/> と入出力をつなぐ。
/// </summary>
public sealed partial class TerminalControl : UserControl
{
    /// <summary>Assets/Terminal を読むための仮想ホスト名。</summary>
    /// <remarks>アプリ同梱の Assets/Terminal を WebView2 から読むために使う。</remarks>
    private const string HostName = "terminal.mmmtool.invalid";

    /// <summary>セッションの依存関係プロパティ</summary>
    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session), typeof(ITerminalSession), typeof(TerminalControl), new PropertyMetadata(null, OnSessionChanged));

    /// <summary>UI スレッドのディスパッチャー</summary>
    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>UI スレッドへ渡す前の出力バッファ。</summary>
    /// <remarks>シェル出力は細切れに届くため、UI スレッドへ渡す前にまとめる。</remarks>
    private readonly StringBuilder _pendingOutput = new();
    /// <summary>出力バッファの排他用ロック</summary>
    private readonly Lock _pendingLock = new();
    /// <summary>UI スレッドへの受け渡しを予約済みか</summary>
    private bool _flushQueued;
    /// <summary>xterm.js へ送ったが、描画の受け取りがまだ返っていない文字数</summary>
    /// <remarks>UI スレッドだけで読み書きする。xterm.js の書き込み待ちがあふれて出力が捨てられるのを防ぐ。</remarks>
    private int _unconfirmedChars;
    /// <summary>シェルを起動し直している最中か</summary>
    /// <remarks>終了待ちの間に押されたキーで、二重に起動し直さないための印。UI スレッドだけで読み書きする。</remarks>
    private bool _isRestarting;

    /// <summary>描画の受け取りを待たずに送ってよい文字数の上限</summary>
    private const int MaxUnconfirmedChars = 1024 * 1024;

    /// <summary>WebView の初期化を始めたか</summary>
    private bool _webViewInitialized;
    /// <summary>xterm.js の準備ができたか</summary>
    private bool _webViewReady;

    /// <summary>xterm.js 側の現在の端末の列数。</summary>
    /// <remarks>再起動時に使う。</remarks>
    private int _columns;
    /// <summary>xterm.js 側の現在の端末の行数</summary>
    private int _rows;

    /// <summary>コントロールを作る</summary>
    public TerminalControl()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Loaded += OnLoaded;
    }

    /// <summary>シェルのセッション</summary>
    public ITerminalSession? Session
    {
        get => (ITerminalSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    /// <summary>セッションが差し替わったら、イベントの購読を付け替える</summary>
    /// <param name="d">変更されたコントロール</param>
    /// <param name="e">変更の情報</param>
    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TerminalControl)d;
        if (e.OldValue is ITerminalSession oldSession)
        {
            oldSession.OutputReceived -= control.OnOutputReceived;
            oldSession.Exited -= control.OnSessionExited;
            oldSession.SubmitRequested -= control.OnSubmitRequested;
        }
        if (e.NewValue is ITerminalSession newSession)
        {
            newSession.OutputReceived += control.OnOutputReceived;
            newSession.Exited += control.OnSessionExited;
            newSession.SubmitRequested += control.OnSubmitRequested;
        }
    }

    /// <summary>送信を求められたら、端末に貼り付けとして渡す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="text">貼り付けるテキスト</param>
    private void OnSubmitRequested(object? sender, string text)
    {
        if (_webViewReady)
        {
            PostMessage("submit", text);
        }
        else
        {
            // 端末の表示がまだ準備できていなければ、そのまま書き込んで確定する
            Session?.Write(text);
            Session?.Write("\r");
        }
    }

    /// <summary>読み込み時に WebView を初期化して、xterm.js のページを開く</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized)
        {
            // ページを再表示したとき
            FocusTerminal();
            return;
        }
        _webViewInitialized = true;

        try
        {
            await WebView.EnsureCoreWebView2Async();
        }
        catch (COMException ex)
        {
            // WebView2 ランタイムが無い・起動できないとき。ターミナルだけが使えないので、他の機能は使えるよう、ここに理由を出す
            ShowInitializationError(ex);
            return;
        }
        var core = WebView.CoreWebView2;

        // ブラウザとしての機能（再読み込み・検索・印刷・ズーム・右クリックメニュー等）を止め、端末として振る舞わせる
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        // ページからアプリ側のオブジェクトを呼べないようにする（メッセージのやり取りだけを使う）
        core.Settings.AreHostObjectsAllowed = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.WebMessageReceived += OnWebMessageReceived;
        core.SetVirtualHostNameToFolderMapping(
            HostName, Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal"), CoreWebView2HostResourceAccessKind.DenyCors);
        core.Navigate($"https://{HostName}/index.html");
    }

    /// <summary>WebView2 の初期化失敗の理由を表示する</summary>
    /// <param name="exception">初期化の失敗</param>
    /// <remarks>
    /// 制約: WebView2 ランタイムが入っていない PC で、どの例外が出るかは、この環境では確かめられていない。COMException 以外が出たときは、未処理例外の受け皿（ログ・ダイアログ・終了）が受ける。実機で確かめて、受ける例外を直す。
    /// </remarks>
    private void ShowInitializationError(COMException exception)
    {
        WebView.Visibility = Visibility.Collapsed;
        InitializationErrorText.Text =
            $"ターミナルを表示できません（WebView2 を初期化できませんでした）。\nWebView2 ランタイムがインストールされているか確認してください。\n\n{exception.Message}";
        InitializationErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>xterm.js からのメッセージを処理する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">受け取ったメッセージの情報</param>
    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!Uri.TryCreate(args.Source, UriKind.Absolute, out var source) || source.Host != HostName)
        {
            return;
        }

        using var json = JsonDocument.Parse(args.WebMessageAsJson);
        var message = json.RootElement;
        switch (message.GetProperty("type").GetString())
        {
            case "ready":
                _columns = message.GetProperty("cols").GetInt32();
                _rows = message.GetProperty("rows").GetInt32();
                OnTerminalReady();
                break;
            case "input":
                OnInput(message.GetProperty("data").GetString() ?? "");
                break;
            case "resize":
                _columns = message.GetProperty("cols").GetInt32();
                _rows = message.GetProperty("rows").GetInt32();
                Session?.Resize(_columns, _rows);
                break;
            case "written":
                _unconfirmedChars = Math.Max(0, _unconfirmedChars - message.GetProperty("length").GetInt32());
                FlushOutput();
                break;
        }
    }

    /// <summary>xterm.js の準備ができたときの処理（シェルを起動する）</summary>
    private void OnTerminalReady()
    {
        _webViewReady = true;

        if (Session is { IsStarted: true } session)
        {
            session.Resize(_columns, _rows);
        }
        else
        {
            StartSessionAsync(restart: false).Forget();
        }

        FlushOutput();
        FocusTerminal();
    }

    /// <summary>端末への入力をシェルへ送る（シェル終了後は再起動する）</summary>
    /// <param name="data">端末への入力</param>
    private void OnInput(string data)
    {
        if (_isRestarting)
        {
            return;
        }

        // シェル終了後は、押されたキーを捨てて再起動のきっかけにする
        if (Session is { HasExited: true })
        {
            StartSessionAsync(restart: true).Forget();
            return;
        }
        Session?.Write(data);
    }

    /// <summary>シェルを起動する（restart が true なら起動し直す）</summary>
    /// <param name="restart">起動し直すなら true</param>
    /// <returns>起動（または起動し直し）の完了を表すタスク</returns>
    /// <remarks>起動し直すときの終了待ちは、UI スレッドを止めずに行う。</remarks>
    private async Task StartSessionAsync(bool restart)
    {
        if (Session is not { } session)
        {
            return;
        }

        try
        {
            if (restart)
            {
                _isRestarting = true;
                // 出力の順序を保つため、シェルの出力と同じ経路で送る
                OnOutputReceived(this, "\r\n");
                await session.RestartAsync(_columns, _rows);
            }
            else
            {
                session.Start(_columns, _rows);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or COMException or InvalidOperationException)
        {
            OnOutputReceived(this, $"\x1b[31mシェルを起動できませんでした: {session.CommandLine}\r\n{ex.Message}\x1b[0m\r\n");
        }
        finally
        {
            _isRestarting = false;
        }
    }

    /// <summary>シェルの出力をためて、UI スレッドで描画する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="text">シェルの出力</param>
    private void OnOutputReceived(object? sender, string text)
    {
        lock (_pendingLock)
        {
            _pendingOutput.Append(text);
            if (_flushQueued)
            {
                return;
            }
            _flushQueued = true;
        }
        _dispatcherQueue.TryEnqueue(FlushOutput);
    }

    /// <summary>シェルが終了したことを端末に表示する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnSessionExited(object? sender, EventArgs e)
        => OnOutputReceived(sender, "\r\n\x1b[90m[プロセスが終了しました。何かキーを押すと再起動します]\x1b[0m\r\n");

    /// <summary>ためた出力を端末へ送る</summary>
    /// <remarks>端末の描画が追いついていない（<see cref="MaxUnconfirmedChars"/> 超）ときは送らず、受け取りが返ってから送る。</remarks>
    private void FlushOutput()
    {
        if (!_webViewReady)
        {
            return;
        }

        string text;
        lock (_pendingLock)
        {
            if (_unconfirmedChars >= MaxUnconfirmedChars)
            {
                _flushQueued = false;
                return;
            }
            text = _pendingOutput.ToString();
            _pendingOutput.Clear();
            _flushQueued = false;
        }

        if (text.Length > 0)
        {
            _unconfirmedChars += text.Length;
            PostMessage("output", text);
        }
    }

    /// <summary>端末にキーボードフォーカスを移す。</summary>
    public void FocusTerminal()
    {
        if (!_webViewReady)
        {
            return;
        }
        WebView.Focus(FocusState.Programmatic);
        PostMessage("focus", null);
    }

    /// <summary>xterm.js へメッセージを送る</summary>
    /// <param name="type">メッセージの種類</param>
    /// <param name="data">メッセージの内容。無ければ null</param>
    private void PostMessage(string type, string? data)
    {
        // トリミング有効の発行でも動くよう、リフレクションを使わずに JSON を組み立てる
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", type);
            if (data is not null)
            {
                writer.WriteString("data", data);
            }
            writer.WriteEndObject();
        }
        WebView.CoreWebView2?.PostWebMessageAsJson(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
