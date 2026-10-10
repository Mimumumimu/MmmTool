namespace MmmTool.WorkItems.Core.Json;

/// <summary>WorkItemChanges.json の中身</summary>
internal sealed class WorkItemChangeFile
{
    /// <summary>変更の記録の一覧 (古いものが先)</summary>
    public List<WorkItemChange>? Items { get; set; } = [];
}
