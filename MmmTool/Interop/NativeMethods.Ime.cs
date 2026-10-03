using System.Runtime.InteropServices;

namespace MmmTool.Interop;

internal static partial class NativeMethods
{
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
}
