namespace MmmTool.WorkItems.Core;

/// <summary>グループ</summary>
/// <remarks>
/// 案件やグループの下に置く、子を持つ行で、保存するデータ。
/// 保存先に依存しない。
/// </remarks>
public sealed record WorkGroup
{
    /// <summary>番号</summary>
    /// <remarks>保存先が決め、0 はまだ保存していない。</remarks>
    public int Id { get; init; }

    /// <summary>属する案件の番号</summary>
    public int ProjectId { get; init; }

    /// <summary>親のグループの番号</summary>
    public int ParentGroupId { get; init; }

    /// <summary>兄弟の中の並び</summary>
    /// <remarks>小さいほど先で、グループと作業にまたがる 1 つの数。</remarks>
    public int SortOrder { get; init; }

    /// <summary>名前</summary>
    public string Name { get; init; } = "";

    /// <summary>リンク</summary>
    /// <remarks>URL・フォルダ・ファイルのパスで、無いときは空文字。</remarks>
    public string Link { get; init; } = "";

    /// <summary>備考</summary>
    public string Note { get; init; } = "";

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
