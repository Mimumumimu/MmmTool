namespace MmmTool.WorkItems.Core.Json;

/// <summary>WorkItems.json の中身</summary>
/// <remarks>ほかのファイルと同じく <c>{ "items": [...] }</c> の形にする。</remarks>
internal sealed class WorkItemFile
{
    /// <summary>行の一覧 (論理削除済みも含む)</summary>
    public List<WorkItem>? Items { get; set; } = [];
}
