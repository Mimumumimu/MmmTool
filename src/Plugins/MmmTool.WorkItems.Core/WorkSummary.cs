namespace MmmTool.WorkItems.Core;

/// <summary>行ごとの表示用の集計値</summary>
/// <param name="StartDate">開始日 (案件・グループは子の開始日の最小)</param>
/// <param name="DueDate">期限日 (案件・グループは子の期限日の最大)</param>
/// <param name="PlannedHours">
/// 予定時間で、案件・グループは子の合計。
/// 1 つも無ければ null。
/// </param>
/// <param name="ActualTotal">
/// 実績で、累計。
/// 全員の、すべての日の合計。
/// </param>
/// <param name="ActualDay">実績 (入力日の、自分の分)。</param>
/// <param name="Progress">
/// 進捗度で、案件・グループは予定時間の重み付き平均か件数の平均。
/// 作業が無ければ null。
/// </param>
/// <param name="WorkCount">下にある作業の数 (作業自身は 1)</param>
/// <param name="IsFinished">
/// 完了しているか。
/// 作業は状態と完了日で判定し、案件・グループは作業が 1 件以上あり、未完了の作業が 1 件も無いとき完了。
/// </param>
/// <param name="CompletedDate">
/// 完了日で、完了しているとき。
/// 案件・グループは下の作業の完了日の最大。
/// 完了していなければ null。
/// </param>
/// <param name="Status">
/// 状態で、案件・グループは下の作業から計算する。
/// すべて未対応なら未対応、すべて完了なら完了、それ以外は作業中。
/// 作業が無ければ null。
/// </param>
/// <remarks>作業は自分の値、案件・グループは下の作業から計算した値。</remarks>
public sealed record WorkSummary(
    DateOnly? StartDate,
    DateOnly? DueDate,
    double? PlannedHours,
    double ActualTotal,
    double ActualDay,
    int? Progress,
    int WorkCount,
    bool IsFinished,
    DateOnly? CompletedDate,
    WorkItemStatus? Status)
{
    /// <summary>完了して日がたったか</summary>
    /// <param name="date">基準の日 (画面では入力日)</param>
    /// <returns>完了していて、完了日が基準の日より前なら true。</returns>
    /// <remarks>完了したグループの既定の畳みと、作業の行を触れなくする判定がこの 1 つの条件を使う。</remarks>
    public bool IsFinishedBefore(DateOnly date) => IsFinished && CompletedDate is { } done && done < date;

    /// <summary>期限を過ぎた日数</summary>
    /// <param name="date">基準の日 (画面では入力日)</param>
    /// <returns>
    /// 未完了で、期限日が基準の日より前なら、過ぎた日数で、1 以上。
    /// それ以外は 0。
    /// </returns>
    /// <remarks>
    /// 期限日の当日は過ぎていない
    /// 完了した作業は期限を過ぎてから完了したものも含めて 0
    /// 未完了の定義は <see cref="IsFinished"/> にある
    /// </remarks>
    public int OverdueDays(DateOnly date) => !IsFinished && DueDate is { } due && due < date ? date.DayNumber - due.DayNumber : 0;
}
