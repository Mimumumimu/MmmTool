namespace MmmTool.WorkItems.Core;

/// <summary>作業 (入力する行。子を持たない。保存するデータ。保存先に依存しない)</summary>
/// <remarks>日付・予定時間・完了日の「無い」は null (保存先では <c>9999-12-31</c>・0 に置き換える)。</remarks>
public sealed record WorkItem
{
    /// <summary>番号 (保存先が決める。0 は、まだ保存していない)</summary>
    public int Id { get; init; }

    /// <summary>属する案件の番号</summary>
    public int ProjectId { get; init; }

    /// <summary>親のグループの番号 (0 は案件の直下)</summary>
    public int GroupId { get; init; }

    /// <summary>兄弟の中の並び (小さいほど先。グループと作業にまたがる 1 つの数)</summary>
    public int SortOrder { get; init; }

    /// <summary>名前 (必須)</summary>
    public string Name { get; init; } = "";

    /// <summary>備考 (作業ごとの共通の備考。永続)</summary>
    public string Note { get; init; } = "";

    /// <summary>優先度</summary>
    public WorkItemPriority Priority { get; init; } = WorkItemPriority.Normal;

    /// <summary>開始日 (無ければ null)</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>期限日 (無ければ null)</summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>状態</summary>
    public WorkItemStatus Status { get; init; } = WorkItemStatus.Open;

    /// <summary>進捗度 (0 から 100 の 10 刻みの整数。ラベルではなく数値を持つ)</summary>
    public int Progress { get; init; }

    /// <summary>予定時間 (作業ごとの共通の値。無ければ null)</summary>
    public double? PlannedHours { get; init; }

    /// <summary>完了日 (状態が完了に変わった入力日。内部の値。完了でなければ null)</summary>
    public DateOnly? CompletedOn { get; init; }

    /// <summary>実績の累計 (その作業の、全員の、すべての日の実績の合計。記録を保存するたびに、保存先が計算し直して書く)</summary>
    public double ActualTotalHours { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時 (保存先の時計)</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>完了した作業か</summary>
    /// <remarks>状態が完了で、完了日がある作業。状態が正で、完了日があっても状態が完了でなければ未完了。完了日が無い行 (手で直したデータなど)も未完了として扱い、隠さない。</remarks>
    public bool IsFinished => Status == WorkItemStatus.Closed && CompletedOn is not null;
}
