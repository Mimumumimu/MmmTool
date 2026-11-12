namespace MmmTool.Backlog.Core;

/// <summary>Backlog の共有ファイル 1 件でAPI の応答の 1 要素</summary>
/// <remarks>
/// 応答の JSON から読むので、プロパティ名は API の名前 (camelCase)に対応する。
/// 大文字小文字は区別しない。
/// 使うものだけを持つ。
/// </remarks>
public sealed class BacklogSharedFile
{
    /// <summary>種別を表す値のうちファイルを指すもの</summary>
    public const string FileType = "file";

    /// <summary>共有ファイルの ID</summary>
    /// <remarks>ダウンロードに使う</remarks>
    public long Id { get; init; }

    /// <summary>種別</summary>
    public string Type { get; init; } = "";

    /// <summary>ファイル名</summary>
    public string Name { get; init; } = "";

    /// <summary>大きさで、単位はバイト</summary>
    /// <remarks>応答に無いときは null</remarks>
    public long? Size { get; init; }

    /// <summary>更新日時</summary>
    /// <remarks>応答に無いときは null</remarks>
    public DateTimeOffset? Updated { get; init; }

    /// <summary>ファイルか</summary>
    /// <remarks>フォルダーではないか</remarks>
    public bool IsFile => string.Equals(Type, FileType, StringComparison.OrdinalIgnoreCase);
}
