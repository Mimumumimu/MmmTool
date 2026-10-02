namespace MmmTool.Services;

/// <summary>画像の変換</summary>
public interface IImageConverter
{
    /// <summary>画像（PNG・BMP 等）を JPEG に変換する。</summary>
    Task<byte[]> ToJpegAsync(Stream image);
}
