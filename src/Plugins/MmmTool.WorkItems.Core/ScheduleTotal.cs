namespace MmmTool.WorkItems.Core;

/// <summary>スケジュールの総合計で、表示している案件を合わせた実績。案件の出し入れに合わせて、足し引きする</summary>
/// <remarks>
/// 案件を 1 つ出し入れしたとき、ほかの案件を足し直さずに済むよう、日ごとの合計を持ち続けて、その案件の分だけを足す・引く。
/// 10 進数で足し引きするので、足した分を引くと、元の値に戻る。
/// </remarks>
public sealed class ScheduleTotal
{
    /// <summary>日ごとの時間の合計 (合計が 0 の日は持たない)</summary>
    private readonly SortedDictionary<DateOnly, decimal> _days = [];

    /// <summary>実績の時間の合計 (丸める前)</summary>
    private decimal _hours;

    /// <summary>実績の時間の合計で、全期間。小数 2 桁に丸めた値</summary>
    public double Hours => (double)Math.Round(_hours, 2, MidpointRounding.AwayFromZero);

    /// <summary>日ごとの合計から、実績の棒を作る</summary>
    /// <returns>実績の棒で、作りは、ほかの行と同じ 1 本。実績が無ければ、空の並び</returns>
    public IReadOnlyList<ScheduleActualRun> Runs() => _days.Count == 0 ? [] : ScheduleActuals.Runs(_days);

    /// <summary>空に戻す</summary>
    public void Clear()
    {
        _days.Clear();
        _hours = 0;
    }

    /// <summary>案件 1 つの分を足す</summary>
    /// <param name="hours">その案件の、実績の時間の合計</param>
    /// <param name="runs">その案件の、実績の棒</param>
    public void Add(double hours, IReadOnlyList<ScheduleActualRun> runs) => Apply(hours, runs, 1);

    /// <summary>足してあった案件 1 つの分を引く</summary>
    /// <param name="hours">足したときと同じ、実績の時間の合計</param>
    /// <param name="runs">足したときと同じ、実績の棒</param>
    public void Remove(double hours, IReadOnlyList<ScheduleActualRun> runs) => Apply(hours, runs, -1);

    /// <summary>案件 1 つの分を、足す・引く</summary>
    /// <param name="hours">実績の時間の合計</param>
    /// <param name="runs">実績の棒</param>
    /// <param name="sign">足すなら 1、引くなら -1</param>
    private void Apply(double hours, IReadOnlyList<ScheduleActualRun> runs, int sign)
    {
        _hours += sign * (decimal)hours;
        foreach (var run in runs)
        {
            for (var i = 0; i < run.Hours.Count; i++)
            {
                if (run.Hours[i] <= 0)
                {
                    continue;
                }

                var date = run.Start.AddDays(i);
                var value = _days.GetValueOrDefault(date) + (sign * (decimal)run.Hours[i]);
                if (value > 0)
                {
                    _days[date] = value;
                }
                else
                {
                    _days.Remove(date);
                }
            }
        }
    }
}
