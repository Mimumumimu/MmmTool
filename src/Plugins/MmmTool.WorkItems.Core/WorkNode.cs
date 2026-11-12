namespace MmmTool.WorkItems.Core;

/// <summary>木構造の 1 つの行で、案件・グループ・作業のどれか</summary>
/// <param name="key">行の識別 (種類 + 番号)</param>
/// <remarks>子・集計値を持つ</remarks>
public sealed class WorkNode(WorkKey key)
{
    /// <summary>行の識別</summary>
    public WorkKey Key { get; } = key;

    /// <summary>案件</summary>
    /// <remarks>種類が案件のとき</remarks>
    public WorkProject? Project { get; init; }

    /// <summary>グループ</summary>
    /// <remarks>種類がグループのとき</remarks>
    public WorkGroup? Group { get; init; }

    /// <summary>作業</summary>
    /// <remarks>種類が作業のとき</remarks>
    public WorkItem? Item { get; init; }

    /// <summary>名前</summary>
    public string Name => Project?.Name ?? Group?.Name ?? Item?.Name ?? "";

    /// <summary>リンク</summary>
    public string Link => Project?.Link ?? Group?.Link ?? Item?.Link ?? "";

    /// <summary>備考</summary>
    public string Note => Project?.Note ?? Group?.Note ?? Item?.Note ?? "";

    /// <summary>子</summary>
    public List<WorkNode> Children { get; } = [];

    /// <summary>集計値</summary>
    /// <remarks><see cref="WorkItemTree.Build"/> が設定する</remarks>
    public WorkSummary Summary { get; internal set; } = new(null, null, null, 0, 0, null, 0, false, null, null);

    /// <summary>この行と下のすべての行を上から順に返す</summary>
    /// <returns>この行から始まる、行の並び。</returns>
    /// <remarks>段数を問わない</remarks>
    public IEnumerable<WorkNode> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var node in child.SelfAndDescendants())
            {
                yield return node;
            }
        }
    }
}
