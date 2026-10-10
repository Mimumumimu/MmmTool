namespace MmmTool.WorkItems.Core.Json;

/// <summary>WorkRecords.json の中身</summary>
internal sealed class WorkRecordFile
{
    /// <summary>日ごとの記録の一覧</summary>
    public List<WorkRecord>? Items { get; set; } = [];
}
