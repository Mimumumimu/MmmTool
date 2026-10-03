using System.Text;
using MmmSdk.WinUI.ConPty;
using MmmTool.Core.CliAssist;

namespace MmmTool.Features.CliAssist.Terminal;

/// <summary>
/// ConPTY（Windows 擬似コンソール）でシェルを起動し、入出力をパイプでやり取りするセッション。
/// </summary>
/// <remarks>擬似コンソールとプロセスの起動は SDK の <see cref="PseudoConsole"/>。ここでは出力の読み取り・終了の通知・入力の確定を受け持つ。</remarks>
public sealed class PseudoConsoleSession : ITerminalSession
{
    /// <summary>読み取り用のバッファの大きさ（バイト）</summary>
    private const int ReadBufferSize = 16 * 1024;

    /// <summary>出力を読み取り終わるのを待つ時間</summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(1);

    /// <summary>起動中の擬似コンソール</summary>
    /// <remarks>起動前・解放後は null。</remarks>
    private PseudoConsole? _console;
    /// <summary>プロセス終了の待機登録</summary>
    private RegisteredWaitHandle? _exitWait;
    /// <summary>出力を読み続けるタスク</summary>
    private Task? _readTask;
    /// <summary>シェルが終了したか</summary>
    private volatile bool _hasExited;
    /// <summary>破棄済みか</summary>
    private bool _disposed;

    /// <inheritdoc />
    public string CommandLine { get; set; } = DefaultShell.GetCommandLine();

    /// <inheritdoc />
    public string WorkingDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <inheritdoc />
    public bool IsStarted => _console is not null;

    /// <inheritdoc />
    public bool HasExited => _hasExited;

    /// <inheritdoc />
    public event EventHandler<string>? OutputReceived;

    /// <inheritdoc />
    public event EventHandler? Exited;

    /// <inheritdoc />
    public event EventHandler<string>? SubmitRequested;

    /// <inheritdoc />
    public void Start(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsStarted)
        {
            throw new InvalidOperationException("セッションはすでに起動しています。");
        }

        _hasExited = false;
        // 起動に失敗したときは、SDK が作った分を片付けてから例外を投げる（未起動の状態のまま）
        var console = PseudoConsole.Start(CommandLine, WorkingDirectory, columns, rows);
        _console = console;
        _exitWait = ThreadPool.RegisterWaitForSingleObject(
            console.ExitHandle, (_, _) => OnProcessExited(), null, Timeout.Infinite, executeOnlyOnce: true);

        var output = console.Output;
        _readTask = Task.Run(() => ReadLoop(output));
    }

    /// <inheritdoc />
    public async Task RestartAsync(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var (console, readTask) = Detach();
        // 終了待ち（最大で数秒）は UI スレッドの外で行う
        await Task.Run(() => Release(console, readTask));
        ObjectDisposedException.ThrowIf(_disposed, this);
        Start(columns, rows);
    }

    /// <inheritdoc />
    public void Write(string text)
    {
        if (_console is not { } console || string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            console.Input.Write(Encoding.UTF8.GetBytes(text));
            console.Input.Flush();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // シェル終了後の入力は捨てる
        }
    }

    /// <inheritdoc />
    public void Submit(string text)
    {
        if (SubmitRequested is { } handler)
        {
            handler(this, text);
        }
        else
        {
            // 表示側が無いとき（未接続）は、そのまま書き込んで確定する
            Write(text);
            Write("\r");
        }
    }

    /// <inheritdoc />
    public void Resize(int columns, int rows) => _console?.Resize(columns, rows);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        OutputReceived = null;
        Exited = null;
        SubmitRequested = null;
        // アプリの終了時に呼ばれる。シェルを確実に終わらせてから抜ける
        var (console, readTask) = Detach();
        Release(console, readTask);
    }

    /// <summary>シェルのプロセスが終了したときの処理</summary>
    private void OnProcessExited()
    {
        _hasExited = true;
        Exited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>現在の擬似コンソールを切り離す（解放は <see cref="Release"/>）</summary>
    /// <returns>切り離した擬似コンソールと、出力を読み続けているタスク（無ければ null）</returns>
    /// <remarks>以降は未起動の状態になる。呼び出しスレッドを止めない処理だけを行う。</remarks>
    private (PseudoConsole? Console, Task? ReadTask) Detach()
    {
        _exitWait?.Unregister(null);
        _exitWait = null;

        var console = _console;
        _console = null;
        var readTask = _readTask;
        _readTask = null;
        return (console, readTask);
    }

    /// <summary>擬似コンソール・パイプ・プロセスを解放する。</summary>
    /// <param name="console">切り離した擬似コンソール</param>
    /// <param name="readTask">出力を読み続けているタスク</param>
    /// <remarks>
    /// シェルがまだ動いていれば終了する。出力を読み続けたまま擬似コンソールを閉じ、読み取りが終わるのを待ってから、パイプを解放する。
    /// 終了待ちで時間がかかることがあるため、UI スレッドからは直接呼ばない。
    /// </remarks>
    private static void Release(PseudoConsole? console, Task? readTask)
    {
        console?.Close();
        readTask?.Wait(ReadTimeout);
        console?.Dispose();
    }

    /// <summary>シェルの出力を読み続けて通知する（パイプが閉じるまで）</summary>
    /// <param name="output">シェルからの出力パイプ</param>
    private void ReadLoop(Stream output)
    {
        var buffer = new byte[ReadBufferSize];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        // 読み取りの切れ目で UTF-8 の多バイト文字が分断されても正しく復元できるよう、状態を持つデコーダを使う
        var decoder = Encoding.UTF8.GetDecoder();

        try
        {
            int read;
            while ((read = output.Read(buffer)) > 0)
            {
                var count = decoder.GetChars(buffer, 0, read, chars, 0);
                if (count > 0)
                {
                    OutputReceived?.Invoke(this, new string(chars, 0, count));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // パイプが閉じられた（セッション終了）
        }
    }
}
