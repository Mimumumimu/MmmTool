namespace MmmTool.ViewModels;

/// <summary>
/// 送信欄に添付したファイル 1 件。
/// </summary>
public sealed class AttachmentItem(string filePath, string displayName)
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
    };

    /// <summary>一時保存先の絶対パス（送信時にこのパスを付け足す）。</summary>
    public string FilePath { get; } = filePath;

    /// <summary>表示名（元のファイル名）。</summary>
    public string DisplayName { get; } = displayName;

    public bool IsImage { get; } = ImageExtensions.Contains(Path.GetExtension(filePath));

    public bool IsNotImage => !IsImage;

    /// <summary>サムネイルに使う画像のパス（画像でなければ null）。</summary>
    public string? ThumbnailPath => IsImage ? FilePath : null;
}
