using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace MmmTool.Converters;

/// <summary>画像ファイルのパスをサムネイル用の画像に変換する</summary>
/// <remarks>ファイルを開いたままにしない（削除できなくなるため）よう、中身をメモリに読み込んでから表示する。</remarks>
public sealed partial class FilePathToImageSourceConverter : IValueConverter
{
    /// <summary>デコードする高さ</summary>
    /// <remarks>表示サイズに合わせて小さく読み込み、メモリを節約する。</remarks>
    public int DecodePixelHeight { get; set; } = 144;

    /// <inheritdoc />
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || path.Length == 0)
        {
            return null;
        }

        var bitmap = new BitmapImage { DecodePixelHeight = DecodePixelHeight };
        _ = LoadAsync(bitmap, path);
        return bitmap;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    /// <summary>ファイルを読み込んで画像に設定する</summary>
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
