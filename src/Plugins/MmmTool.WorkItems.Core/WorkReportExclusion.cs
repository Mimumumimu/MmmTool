namespace MmmTool.WorkItems.Core;

/// <summary>作業を作業報告のどの部分から外すか</summary>
/// <remarks>
/// 値は保存先の列 <c>WorkItem.ReportExclusion</c> にそのまま書く
/// 値の意味を変えない
/// 外す範囲が増えるときは値を足す
/// </remarks>
public enum WorkReportExclusion : byte
{
    /// <summary>外さない</summary>
    /// <remarks>実績にも予定にも出る</remarks>
    None = 0,

    /// <summary>予定から外す</summary>
    /// <remarks>実績には工数を記入した日や完了にした日があれば出る</remarks>
    Plan = 1,

    /// <summary>実績と予定の両方から外す</summary>
    PlanAndActual = 2,
}
