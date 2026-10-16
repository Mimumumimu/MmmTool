namespace MmmTool.WorkItems.Core;

/// <summary>連続した日の実績 (スケジュールの実績の棒 1 本分)</summary>
/// <param name="Start">棒の最初の日</param>
/// <param name="Hours">日ごとの時間 (最初の日から、連続した日の分。どの日も 0 より大きい)</param>
public sealed record ScheduleActualRun(DateOnly Start, IReadOnlyList<double> Hours)
{
    /// <summary>棒の最後の日</summary>
    public DateOnly End => Start.AddDays(Hours.Count - 1);
}
