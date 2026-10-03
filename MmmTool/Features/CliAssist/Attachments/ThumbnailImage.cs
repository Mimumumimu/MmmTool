using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace MmmTool.Features.CliAssist.Attachments;

/// <summary>添付のサムネイル画像</summary>
/// <remarks>XAML の <c>x:Bind</c> から関数として呼ぶ。ファイルを開いたままにしない（削除できなくなるため）よう、中身をメモリに読み込んでから表示する。</remarks>
public static class ThumbnailImage
{
    /// <summary>デコードする高さ</summary>
    /// <remarks>表示サイズに合わせて小さく読み込み、メモリを節約する。</remarks>
    private const int DecodePixelHeight = 144;

    /// <summary>画像ファイルからサムネイルを作る</summary>
    /// <param name="path">画像ファイルのパス</param>
    /// <returns>サムネイル（読み込みは後から終わる）。パスが空なら null</returns>
    public static BitmapImage? FromFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var bitmap = new BitmapImage { DecodePixelHeight = DecodePixelHeight };
        _ = LoadAsync(bitmap, path);
        return bitmap;
    }

    /// <summary>ファイルを読み込んで画像に設定する</summary>
    /// <param name="bitmap">設定先の画像</param>
    /// <param name="path">画像ファイルのパス</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    private static async Task LoadAsync(BitmapImage bitmap, string path)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            await bitmap.SetSourceAsync(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            // サムネイルが出せないだけなので、空のまま表示する（添付自体は有効）
        }
    }
}
