using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using MmmTool.Services.Terminal;

namespace MmmTool.Views.Controls;

/// <summary>
/// WebView2 上の xterm.js で端末を描画し、<see cref="ITerminalSession"/> と入出力をつなぐ。
/// </summary>
public sealed partial class TerminalControl : UserControl
{
    // アプリ同梱の Assets/Terminal を WebView2 から読むための仮想ホスト名
    private const string HostName = "terminal.mmmtool.invalid";

    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session), typeof(ITerminalSession), typeof(TerminalControl), new PropertyMetadata(null, OnSessionChanged));

    private readonly DispatcherQueue _dispatcherQueue;

    // シェル出力は細切れに届くため、UI スレッドへ渡す前にまとめる
    private readonly StringBuilder _pendingOutput = new();
    private readonly Lock _pendingLock = new();
    private bool _flushQueued;

    private bool _webViewInitialized;
    private bool _webViewReady;

    // xterm.js 側の現在の端末サイズ（再起動時に使う）
    private int _columns;
    private int _rows;

    public TerminalControl()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Loaded += OnLoaded;
    }

    public ITerminalSession? Session
    {
        get => (ITerminalSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TerminalControl)d;
        if (e.OldValue is ITerminalSession oldSession)
        {
            oldSession.OutputReceived -= control.OnOutputReceived;
            oldSession.Exited -= control.OnSessionExited;
        }
        if (e.NewValue is ITerminalSession newSession)
        {
            newSession.OutputReceived += control.OnOutputReceived;
            newSession.Exited += control.OnSessionExited;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized)
        {
            // ページを再表示したとき
            FocusTerminal();
            return;
        }
        _webViewInitialized = true;

        await WebView.EnsureCoreWebView2Async();
        var core = WebView.CoreWebView2;

        // ブラウザとしての機能（再読み込み・検索・印刷・ズーム・右クリックメニュー等）を止め、端末として振る舞わせる
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.WebMessageReceived += OnWebMessageReceived;
        core.SetVirtualHostNameToFolderMapping(
            HostName, Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal"), CoreWebView2HostResourceAccessKind.DenyCors);
        core.Navigate($"https://{HostName}/index.html");
    }

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
        }
    }

    private void OnTerminalReady()
    {
        _webViewReady = true;

        if (Session is { IsStarted: true } session)
        {
            session.Resize(_columns, _rows);
        }
        else
        {
            StartSession(restart: false);
        }

        FlushOutput();
        FocusTerminal();
    }

    private void OnInput(string data)
    {
        // シェル終了後は、押されたキーを捨てて再起動のきっかけにする
        if (Session is { HasExited: true })
        {
            StartSession(restart: true);
            return;
        }
        Session?.Write(data);
    }

    private void StartSession(bool restart)
    {
        if (Session is not { } session)
        {
            return;
        }

        try
        {
            if (restart)
            {
                PostMessage("output", "\r\n");
                session.Restart(_columns, _rows);
            }
            else
            {
                session.Start(_columns, _rows);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or COMException or InvalidOperationException)
        {
            PostMessage("output", $"\x1b[31mシェルを起動できませんでした: {session.CommandLine}\r\n{ex.Message}\x1b[0m\r\n");
        }
    }

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

    private void OnSessionExited(object? sender, EventArgs e)
        => OnOutputReceived(sender, "\r\n\x1b[90m[プロセスが終了しました。何かキーを押すと再起動します]\x1b[0m\r\n");

    private void FlushOutput()
    {
        if (!_webViewReady)
        {
            return;
        }

        string text;
        lock (_pendingLock)
        {
            text = _pendingOutput.ToString();
            _pendingOutput.Clear();
            _flushQueued = false;
        }

        if (text.Length > 0)
        {
            PostMessage("output", text);
        }
    }

    private void FocusTerminal()
    {
        if (!_webViewReady)
        {
            return;
        }
        WebView.Focus(FocusState.Programmatic);
        PostMessage("focus", null);
    }

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
