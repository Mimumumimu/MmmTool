namespace MmmTool.WorkItems.Core;

/// <summary>行ごとの、表示用の集計値 (作業は自分の値、案件・グループは下の作業から計算した値)</summary>
/// <param name="StartDate">開始日 (案件・グループは、子の開始日の最小)</param>
/// <param name="DueDate">期限日 (案件・グループは、子の期限日の最大)</param>
/// <param name="PlannedHours">予定時間 (案件・グループは、子の合計。1 つも無ければ null)</param>
/// <param name="ActualTotal">実績 (累計。全員の、すべての日の合計)</param>
/// <param name="ActualDay">実績 (入力日の、自分の分)</param>
/// <param name="Progress">進捗度 (案件・グループは、予定時間の重み付き平均か件数の平均。作業が無ければ null)</param>
/// <param name="WorkCount">下にある作業の数 (作業自身は 1)</param>
/// <param name="IsFinished">完了しているか (作業は状態と完了日。案件・グループは、作業が 1 件以上あり、未完了の作業が 1 件も無い)</param>
/// <param name="CompletedOn">完了日 (完了しているとき。案件・グループは、下の作業の完了日の最大。完了していなければ null)</param>
/// <param name="Status">状態 (案件・グループは、下の作業から計算する。すべて未対応なら未対応、すべて完了なら完了、それ以外は作業中。作業が無ければ null)</param>
public sealed record WorkSummary(
    DateOnly? StartDate,
    DateOnly? DueDate,
    double? PlannedHours,
    double ActualTotal,
    double ActualDay,
    int? Progress,
    int WorkCount,
    bool IsFinished,
    DateOnly? CompletedOn,
    WorkItemStatus? Status);
