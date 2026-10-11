namespace MmmTool.WorkItems.Core;

/// <summary>木構造の 1 つの行 (案件・グループ・作業のどれか。子・集計値を持つ)</summary>
/// <param name="key">行の識別 (種類 + 番号)</param>
public sealed class WorkNode(WorkKey key)
{
    /// <summary>行の識別</summary>
    public WorkKey Key { get; } = key;

    /// <summary>案件 (種類が案件のとき)</summary>
    public WorkProject? Project { get; init; }

    /// <summary>グループ (種類がグループのとき)</summary>
    public WorkGroup? Group { get; init; }

    /// <summary>作業 (種類が作業のとき)</summary>
    public WorkItem? Item { get; init; }

    /// <summary>名前</summary>
    public string Name => Project?.Name ?? Group?.Name ?? Item?.Name ?? "";

    /// <summary>備考</summary>
    public string Note => Project?.Note ?? Group?.Note ?? Item?.Note ?? "";

    /// <summary>子 (並び順)</summary>
    public List<WorkNode> Children { get; } = [];

    /// <summary>集計値 (<see cref="WorkItemTree.Build"/> が設定する)</summary>
    public WorkSummary Summary { get; internal set; } = new(null, null, null, 0, 0, null, 0, false, null, null);

    /// <summary>この行と、下のすべての行 (段数を問わない)を、上から順に返す</summary>
    /// <returns>この行から始まる、行の並び</returns>
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
