using System.Runtime.InteropServices;

namespace MmmTool.Interop;

internal static partial class NativeMethods
{
    /// <summary>ウィンドウを前面に出す</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>前面に出せれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    /// <summary>ウィンドウの DPI（ウィンドウがあるモニターの DPI）</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>DPI（100% で 96）</returns>
    /// <remarks>表示する前に大きさを決めるために使う（XamlRoot は表示するまで無いため）。96 が 100%。</remarks>
    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hWnd);
}
