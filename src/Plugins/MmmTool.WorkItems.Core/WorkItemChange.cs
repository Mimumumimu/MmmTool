namespace MmmTool.WorkItems.Core;

/// <summary>作業の項目を変えたときの、自動の記録 (1 項目につき 1 件)</summary>
public sealed record WorkItemChange
{
    /// <summary>作業の番号</summary>
    public int WorkItemId { get; init; }

    /// <summary>変えた日時</summary>
    public DateTimeOffset ChangedAt { get; init; }

    /// <summary>項目の名前 (画面に出す名前)</summary>
    public string Field { get; init; } = "";

    /// <summary>変更前の値 (画面に出す形)</summary>
    public string OldValue { get; init; } = "";

    /// <summary>変更後の値 (画面に出す形)</summary>
    public string NewValue { get; init; } = "";
}
