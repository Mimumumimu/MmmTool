namespace MmmTool.Features.CliAssist.Main;

/// <summary>
/// 送信欄に添付したファイル 1 件。
/// </summary>
/// <param name="filePath">送信するファイルの絶対パス</param>
/// <param name="displayName">表示名 (元のファイル名)</param>
/// <param name="isTemporary">一時保存したファイルか</param>
/// <remarks>
/// 元がディスク上のファイル (ドロップ・エクスプローラーからの貼り付け)は、元のパスをそのまま持つ。
/// 元がファイルではないもの (クリップボードの画像など)は、一時保存先のパスを持つ。
/// </remarks>
public sealed class AttachmentItem(string filePath, string displayName, bool isTemporary)
{
    /// <summary>画像として扱う拡張子</summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
    };

    /// <summary>送信するファイルの絶対パス (送信時にこのパスを付け足す)。</summary>
    public string FilePath { get; } = filePath;

    /// <summary>表示名 (元のファイル名)。</summary>
    public string DisplayName { get; } = displayName;

    /// <summary>一時保存したファイルか。</summary>
    /// <remarks>true のときだけ、取り除くときにファイルを削除する (元の場所のファイルは決して消さない)。</remarks>
    public bool IsTemporary { get; } = isTemporary;

    /// <summary>画像かどうか</summary>
    public bool IsImage { get; } = ImageExtensions.Contains(Path.GetExtension(filePath));

    /// <summary>画像ではないか</summary>
    public bool IsNotImage => !IsImage;

    /// <summary>サムネイルに使う画像のパス (画像でなければ null)。</summary>
    public string? ThumbnailPath => IsImage ? FilePath : null;
}
