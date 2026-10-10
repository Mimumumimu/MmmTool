namespace MmmTool.WorkItems.Core.Json;

/// <summary>WorkProgressLevels.json の中身</summary>
internal sealed class WorkProgressFile
{
    /// <summary>進捗度の値ごとのラベル (0 から 100 の 11 件)</summary>
    public List<WorkProgressLabel>? Items { get; set; } = [];
}
