namespace MmmTool.WorkItems.Core;

/// <summary>種類・優先度・状態の、画面に出す名前と、選択肢の一覧</summary>
public static class WorkItemNames
{
    /// <summary>優先度の選択肢 (高い順)</summary>
    public static IReadOnlyList<WorkItemPriority> Priorities { get; } = [WorkItemPriority.High, WorkItemPriority.Normal, WorkItemPriority.Low];

    /// <summary>状態の選択肢 (進む順)</summary>
    public static IReadOnlyList<WorkItemStatus> Statuses { get; } = [WorkItemStatus.Open, WorkItemStatus.InProgress, WorkItemStatus.Resolved, WorkItemStatus.Closed];

    /// <summary>優先度の名前を返す</summary>
    /// <param name="priority">優先度</param>
    /// <returns>画面に出す名前</returns>
    public static string Of(WorkItemPriority priority) => priority switch
    {
        WorkItemPriority.High => "高",
        WorkItemPriority.Low => "低",
        _ => "中",
    };

    /// <summary>状態の名前を返す</summary>
    /// <param name="status">状態</param>
    /// <returns>画面に出す名前</returns>
    public static string Of(WorkItemStatus status) => status switch
    {
        WorkItemStatus.InProgress => "作業中",
        WorkItemStatus.Resolved => "処理済み",
        WorkItemStatus.Closed => "完了",
        _ => "未対応",
    };

    /// <summary>種類の名前を返す</summary>
    /// <param name="kind">種類</param>
    /// <returns>画面に出す名前</returns>
    public static string Of(WorkItemKind kind) => kind == WorkItemKind.Group ? "グループ" : "作業";
}
