namespace MmmTool.WorkItems.Core;

/// <summary>状態 (未対応・作業予定・作業中・完了の 4 つ)</summary>
/// <remarks>値はコードマスタ (区分 1)のコード番号と同じ。表示順と同じ並びにそろえてある ([work-report.md](docs/specs/work-report.md))。</remarks>
public enum WorkItemStatus
{
    /// <summary>未対応 (既定)</summary>
    Open = 1,

    /// <summary>作業予定 (作業報告の予定に出す)</summary>
    Planned = 2,

    /// <summary>作業中</summary>
    InProgress = 3,

    /// <summary>完了</summary>
    Closed = 4,
}
