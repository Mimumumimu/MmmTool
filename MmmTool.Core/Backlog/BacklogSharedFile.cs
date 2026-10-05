namespace MmmTool.Core.Backlog;

/// <summary>
/// Backlog の共有ファイル 1 件 (API の応答の 1 要素)。
/// </summary>
/// <remarks>
/// 応答の JSON から読むので、プロパティ名は API の名前 (camelCase)に対応する (大文字小文字は区別しない)。使うものだけを持つ。
/// </remarks>
public sealed class BacklogSharedFile
{
    /// <summary>種別を表す値のうち、ファイルを指すもの</summary>
    public const string FileType = "file";

    /// <summary>共有ファイルの ID (ダウンロードに使う)</summary>
    public long Id { get; init; }

    /// <summary>種別 (<c>file</c> / <c>directory</c>)</summary>
    public string Type { get; init; } = "";

    /// <summary>ファイル名</summary>
    public string Name { get; init; } = "";

    /// <summary>大きさ (バイト)。応答に無い (null)ときは null</summary>
    public long? Size { get; init; }

    /// <summary>更新日時。応答に無いときは null</summary>
    public DateTimeOffset? Updated { get; init; }

    /// <summary>ファイルか (フォルダーではないか)</summary>
    public bool IsFile => string.Equals(Type, FileType, StringComparison.OrdinalIgnoreCase);
}
