namespace MmmTool.Features.CliAssist.Attachments;

/// <summary>画像の変換</summary>
public interface IImageConverter
{
    /// <summary>画像（PNG・BMP 等）を JPEG に変換する。</summary>
    /// <param name="image">変換する画像のストリーム</param>
    /// <returns>JPEG のバイト列</returns>
    Task<byte[]> ToJpegAsync(Stream image);
}
