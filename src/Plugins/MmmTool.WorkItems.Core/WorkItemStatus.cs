using System.Text.Json.Serialization;

namespace MmmTool.WorkItems.Core;

/// <summary>状態 (Backlog 標準の 4 つ。連携で変換しないため、名前も同じにそろえる)</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkItemStatus>))]
public enum WorkItemStatus
{
    /// <summary>未対応 (既定)</summary>
    Open = 1,

    /// <summary>作業中</summary>
    InProgress = 2,

    /// <summary>処理済み</summary>
    Resolved = 3,

    /// <summary>完了</summary>
    Closed = 4,
}
