namespace MmmTool.WorkItems.Core;

/// <summary>スケジュールに出す内容 (行の木と、実績の棒と、日付の軸の範囲)</summary>
/// <param name="Roots">案件の行の並び (「自分の担当だけ」のときは、自分が担当でない作業の行を外したもの。合計は、全員の作業から計算したまま)</param>
/// <param name="Actuals">行ごとの、実績の棒 (実績が無い行は含まれない)</param>
/// <param name="DayNotes">作業ごとの、日の備考 (備考が無い作業と、案件・グループは含まれない)</param>
/// <param name="AxisStart">軸の最初の日</param>
/// <param name="AxisEnd">軸の最後の日</param>
public sealed record ScheduleSnapshot(
    IReadOnlyList<WorkNode> Roots,
    IReadOnlyDictionary<WorkKey, IReadOnlyList<ScheduleActualRun>> Actuals,
    IReadOnlyDictionary<WorkKey, IReadOnlyDictionary<DateOnly, string>> DayNotes,
    DateOnly AxisStart,
    DateOnly AxisEnd)
{
    /// <summary>軸の範囲を求める</summary>
    /// <param name="roots">案件の行の並び (絞り込む前)</param>
    /// <param name="actuals">行ごとの実績の棒</param>
    /// <param name="today">今日 (必ず範囲に入れる。今日の線と「今日」ボタンのため)</param>
    /// <returns>軸の最初の日と最後の日</returns>
    /// <remarks>
    /// 読んだ作業の開始日・期限日・完了日・実績の日と今日のうち、いちばん早い日の月初めから、いちばん遅い日の月末まで (余白は足さない)。
    /// 日付が 1 つも無いときは、今日の月の月初めから月末まで。日付が「ない」(null)の行は、範囲に入れない。
    /// </remarks>
    public static (DateOnly Start, DateOnly End) Range(IReadOnlyList<WorkNode> roots, IReadOnlyDictionary<WorkKey, IReadOnlyList<ScheduleActualRun>> actuals, DateOnly today)
    {
        DateOnly? min = null;
        DateOnly? max = null;
        void Include(DateOnly? date)
        {
            if (date is not { } value)
            {
                return;
            }
            min = min is { } lo && lo <= value ? lo : value;
            max = max is { } hi && hi >= value ? hi : value;
        }

        foreach (var node in roots.SelectMany(r => r.SelfAndDescendants()))
        {
            Include(node.Summary.StartDate);
            Include(node.Summary.DueDate);
            Include(node.Summary.CompletedDate);
        }
        foreach (var run in actuals.Values.SelectMany(runs => runs))
        {
            Include(run.Start);
            Include(run.End);
        }

        Include(today);
        var first = min!.Value;
        var last = max!.Value;
        return (new DateOnly(first.Year, first.Month, 1), new DateOnly(last.Year, last.Month, DateTime.DaysInMonth(last.Year, last.Month)));
    }
}
