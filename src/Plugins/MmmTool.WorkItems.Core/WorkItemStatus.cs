namespace MmmTool.WorkItems.Core;

/// <summary>状態 (未対応・作業中・完了の 3 つ)</summary>
public enum WorkItemStatus
{
    /// <summary>未対応 (既定)</summary>
    Open = 1,

    /// <summary>作業中</summary>
    InProgress = 2,

    /// <summary>完了</summary>
    Closed = 3,
}
