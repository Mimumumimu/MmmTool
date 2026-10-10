namespace MmmTool.WorkItems.Core;

/// <summary>行ごとの、表示用の集計値 (作業は自分の値、グループは下の作業から計算した値)</summary>
/// <param name="StartDate">開始日 (グループは、子の開始日の最小)</param>
/// <param name="DueDate">期限日 (グループは、子の期限日の最大)</param>
/// <param name="PlannedHours">予定時間 (グループは、子の合計。1 つも無ければ null)</param>
/// <param name="ActualTotal">実績 (累計。すべての日の合計)</param>
/// <param name="ActualDay">実績 (入力日の分)</param>
/// <param name="Progress">進捗度 (グループは、予定時間の重み付き平均か件数の平均。作業が無ければ null)</param>
public sealed record WorkItemSummary(
    DateOnly? StartDate,
    DateOnly? DueDate,
    double? PlannedHours,
    double ActualTotal,
    double ActualDay,
    int? Progress);
