using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static MmmTool.Interop.NativeMethods;

namespace MmmTool.Services.Terminal;

/// <summary>
/// ConPTY（Windows 擬似コンソール）でシェルを起動し、入出力をパイプでやり取りするセッション。
/// </summary>
public sealed class PseudoConsoleSession : ITerminalSession
{
    private nint _pseudoConsole;
    private FileStream? _input;
    private FileStream? _output;
    private SafeWaitHandle? _process;
    private RegisteredWaitHandle? _exitWait;
    private Task? _readTask;
    private volatile bool _hasExited;
    private bool _disposed;

    public string CommandLine { get; set; } = DefaultShell.GetCommandLine();

    public string WorkingDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public bool IsStarted => _pseudoConsole != 0;

    public bool HasExited => _hasExited;

    public event EventHandler<string>? OutputReceived;

    public event EventHandler? Exited;

    public void Start(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsStarted)
        {
            throw new InvalidOperationException("セッションはすでに起動しています。");
        }

        if (!CreatePipe(out var inputRead, out var inputWrite, 0, 0)
            || !CreatePipe(out var outputRead, out var outputWrite, 0, 0))
        {
            throw new Win32Exception();
        }

        // ConPTY 側の端（入力の読み取り側・出力の書き込み側）は、作成後に ConPTY が保持するので閉じてよい
        using (inputRead)
        using (outputWrite)
        {
            Marshal.ThrowExceptionForHR(CreatePseudoConsole(ToCoord(columns, rows), inputRead, outputWrite, 0, out _pseudoConsole));
        }

        _input = new FileStream(inputWrite, FileAccess.Write, 0);
        _output = new FileStream(outputRead, FileAccess.Read, 0);

        _hasExited = false;
        try
        {
            _process = StartProcess();
        }
        catch
        {
            // シェルを起動できなかったときは擬似コンソールも片付け、未起動の状態に戻す
            Close();
            throw;
        }
        _exitWait = ThreadPool.RegisterWaitForSingleObject(
            new ProcessWaitHandle(_process), (_, _) => OnProcessExited(), null, Timeout.Infinite, executeOnlyOnce: true);

        var output = _output;
        _readTask = Task.Run(() => ReadLoop(output));
    }

    public void Restart(int columns, int rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Close();
        Start(columns, rows);
    }

    public void Write(string text)
    {
        if (_input is null || string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            _input.Write(Encoding.UTF8.GetBytes(text));
            _input.Flush();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // シェル終了後の入力は捨てる
        }
    }

    public void Resize(int columns, int rows)
    {
        if (IsStarted)
        {
            ResizePseudoConsole(_pseudoConsole, ToCoord(columns, rows));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        OutputReceived = null;
        Exited = null;
        Close();
    }

    private void OnProcessExited()
    {
        _hasExited = true;
        Exited?.Invoke(this, EventArgs.Empty);
    }

    // 擬似コンソール・パイプ・プロセスを解放する（シェルがまだ動いていれば終了する）
    private void Close()
    {
        _exitWait?.Unregister(null);
        _exitWait = null;
        _input?.Dispose();
        _input = null;

        if (_pseudoConsole != 0)
        {
            var pseudoConsole = _pseudoConsole;
            _pseudoConsole = 0;

            // 出力を読み続けていないと ClosePseudoConsole が戻らない場合があるため、読み取りを止めずに別スレッドで閉じる
            Task.Run(() => ClosePseudoConsole(pseudoConsole)).Wait(TimeSpan.FromSeconds(3));
        }

        _readTask?.Wait(TimeSpan.FromSeconds(1));
        _readTask = null;
        _output?.Dispose();
        _output = null;
        _process?.Dispose();
        _process = null;
    }

    private unsafe SafeWaitHandle StartProcess()
    {
        nint size = 0;
        InitializeProcThreadAttributeList(0, 1, 0, ref size);
        var attributeList = Marshal.AllocHGlobal(size);
        try
        {
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                throw new Win32Exception();
            }

            try
            {
                if (!UpdateProcThreadAttribute(attributeList, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, _pseudoConsole, nint.Size, 0, 0))
                {
                    throw new Win32Exception();
                }

                var startupInfo = new STARTUPINFOEXW { lpAttributeList = attributeList };
                startupInfo.StartupInfo.cb = Unsafe.SizeOf<STARTUPINFOEXW>();
                // 親（このアプリ）の標準ハンドルを子へ引き継がせない
                startupInfo.StartupInfo.dwFlags = STARTF_USESTDHANDLES;

                var commandLine = (CommandLine + '\0').ToCharArray();
                PROCESS_INFORMATION processInfo;
                fixed (char* commandLinePtr = commandLine)
                {
                    if (!CreateProcess(null, commandLinePtr, 0, 0, false, EXTENDED_STARTUPINFO_PRESENT, 0,
                            WorkingDirectory, ref startupInfo, out processInfo))
                    {
                        throw new Win32Exception();
                    }
                }

                CloseHandle(processInfo.hThread);
                return new SafeWaitHandle(processInfo.hProcess, ownsHandle: true);
            }
            finally
            {
                DeleteProcThreadAttributeList(attributeList);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(attributeList);
        }
    }

    private void ReadLoop(FileStream output)
    {
        var buffer = new byte[16 * 1024];
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

    private static COORD ToCoord(int columns, int rows)
        => new() { X = (short)Math.Clamp(columns, 1, short.MaxValue), Y = (short)Math.Clamp(rows, 1, short.MaxValue) };

    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeWaitHandle handle)
        {
            // 待機用に借りるだけで、ハンドルの所有はセッション側
            SafeWaitHandle = new SafeWaitHandle(handle.DangerousGetHandle(), ownsHandle: false);
        }
    }
}
