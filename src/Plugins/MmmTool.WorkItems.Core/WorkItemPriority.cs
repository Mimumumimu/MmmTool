using System.Text.Json.Serialization;

namespace MmmTool.WorkItems.Core;

/// <summary>優先度 (Backlog と同じ 3 段階)</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkItemPriority>))]
public enum WorkItemPriority
{
    /// <summary>高</summary>
    High = 1,

    /// <summary>中 (既定)</summary>
    Normal = 2,

    /// <summary>低</summary>
    Low = 3,
}
