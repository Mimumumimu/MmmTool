using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MmmTool.Interop;

internal static partial class NativeMethods
{
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
}
