namespace MmmTool.Services;

public interface IImageConverter
{
    /// <summary>画像（PNG・BMP 等）を JPEG に変換する。</summary>
    Task<byte[]> ToJpegAsync(Stream image);
}
