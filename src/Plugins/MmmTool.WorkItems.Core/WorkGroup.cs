namespace MmmTool.WorkItems.Core;

/// <summary>グループ (案件やグループの下に置く、子を持つ行。保存するデータ。保存先に依存しない)</summary>
public sealed record WorkGroup
{
    /// <summary>番号 (保存先が決める。0 は、まだ保存していない)</summary>
    public int Id { get; init; }

    /// <summary>属する案件の番号</summary>
    public int ProjectId { get; init; }

    /// <summary>親のグループの番号 (0 は案件の直下)</summary>
    public int ParentGroupId { get; init; }

    /// <summary>兄弟の中の並び (小さいほど先。グループと作業にまたがる 1 つの数)</summary>
    public int SortOrder { get; init; }

    /// <summary>名前 (必須)</summary>
    public string Name { get; init; } = "";

    /// <summary>リンク (URL・フォルダ・ファイルのパス。無いときは空文字)</summary>
    public string Link { get; init; } = "";

    /// <summary>備考</summary>
    public string Note { get; init; } = "";

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時 (保存先の時計)</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
