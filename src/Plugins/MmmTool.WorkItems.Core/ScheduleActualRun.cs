namespace MmmTool.WorkItems.Core;

/// <summary>最初の実績の日から最後の実績の日までの実績 (スケジュールの実績の棒 1 本分)</summary>
/// <param name="Start">棒の最初の日</param>
/// <param name="Hours">日ごとの時間 (最初の日から、最後の日までの全部の日の分。実績の無い日は 0)</param>
public sealed record ScheduleActualRun(DateOnly Start, IReadOnlyList<double> Hours)
{
    /// <summary>棒の最後の日</summary>
    public DateOnly End => Start.AddDays(Hours.Count - 1);
}
