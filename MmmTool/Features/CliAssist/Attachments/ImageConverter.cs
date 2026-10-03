using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MmmTool.Features.CliAssist.Attachments;

/// <summary>画像を JPEG に変換する</summary>
public sealed class ImageConverter : IImageConverter
{
    /// <inheritdoc />
    public async Task<byte[]> ToJpegAsync(Stream image)
    {
        var decoder = await BitmapDecoder.CreateAsync(image.AsRandomAccessStream());
        // JPEG は透過を持てないので、アルファは無視して変換する
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        var bytes = new byte[output.Size];
        using var reader = new DataReader(output.GetInputStreamAt(0));
        await reader.LoadAsync((uint)output.Size);
        reader.ReadBytes(bytes);
        return bytes;
    }
}
