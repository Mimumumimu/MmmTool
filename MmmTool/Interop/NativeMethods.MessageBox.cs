using System.Runtime.InteropServices;

namespace MmmTool.Interop;

internal static partial class NativeMethods
{
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
}
