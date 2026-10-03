namespace MmmTool.Features.CliAssist;

/// <summary>
/// 送信欄に添付したファイル 1 件。
/// </summary>
/// <param name="filePath">一時保存先の絶対パス</param>
/// <param name="displayName">表示名（元のファイル名）</param>
public sealed class AttachmentItem(string filePath, string displayName)
{
    /// <summary>画像として扱う拡張子</summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
    };

    /// <summary>一時保存先の絶対パス（送信時にこのパスを付け足す）。</summary>
    public string FilePath { get; } = filePath;

    /// <summary>表示名（元のファイル名）。</summary>
    public string DisplayName { get; } = displayName;

    /// <summary>画像かどうか</summary>
    public bool IsImage { get; } = ImageExtensions.Contains(Path.GetExtension(filePath));

    /// <summary>画像ではないか</summary>
    public bool IsNotImage => !IsImage;

    /// <summary>サムネイルに使う画像のパス（画像でなければ null）。</summary>
    public string? ThumbnailPath => IsImage ? FilePath : null;
}
