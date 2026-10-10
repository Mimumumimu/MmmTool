namespace MmmTool.WorkItems.Core;

/// <summary>木構造の 1 つの行 (行・子・集計値)</summary>
/// <param name="item">行</param>
public sealed class WorkItemNode(WorkItem item)
{
    /// <summary>行</summary>
    public WorkItem Item { get; } = item;

    /// <summary>子 (並び順)</summary>
    public List<WorkItemNode> Children { get; } = [];

    /// <summary>集計値 (<see cref="WorkItemTree.Build"/> が設定する)</summary>
    public WorkItemSummary Summary { get; internal set; } = new(null, null, null, 0, 0, null);

    /// <summary>この行と、下のすべての行 (段数を問わない)を、上から順に返す</summary>
    /// <returns>この行から始まる、行の並び</returns>
    public IEnumerable<WorkItemNode> SelfAndDescendants()
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
