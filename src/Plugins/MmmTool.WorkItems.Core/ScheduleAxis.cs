namespace MmmTool.WorkItems.Core;

/// <summary>スケジュールの日付の軸 (範囲・単位・1 日の幅と、日付と位置の変換)</summary>
/// <param name="Start">軸の最初の日 (週は月曜、月は 1 日に合わせてある)</param>
/// <param name="End">軸の最後の日 (週は日曜、月は月末に合わせてある)</param>
/// <param name="Unit">単位</param>
/// <remarks>作るときは <see cref="Create"/> を使う (単位の区切りに合わせる)。同じ範囲・単位なら、等しい。</remarks>
public sealed record ScheduleAxis(DateOnly Start, DateOnly End, ScheduleUnit Unit)
{
    /// <summary>軸を作る</summary>
    /// <param name="start">範囲の最初の日 (この日を含む)</param>
    /// <param name="end">範囲の最後の日 (この日を含む)</param>
    /// <param name="unit">単位</param>
    /// <returns>単位の区切りに合わせた軸 (週は月曜から日曜まで、月は 1 日から月末まで)</returns>
    public static ScheduleAxis Create(DateOnly start, DateOnly end, ScheduleUnit unit)
    {
        if (end < start)
        {
            end = start;
        }

        return unit switch
        {
            ScheduleUnit.Week => new ScheduleAxis(start.AddDays(-MondayIndex(start)), end.AddDays(6 - MondayIndex(end)), unit),
            ScheduleUnit.Month => new ScheduleAxis(new DateOnly(start.Year, start.Month, 1), new DateOnly(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month)), unit),
            _ => new ScheduleAxis(start, end, unit),
        };
    }

    /// <summary>1 日の幅 (単位ごとの固定の値。日は数字が入る幅、週・月は狭い)</summary>
    public double DayWidth => Unit switch
    {
        ScheduleUnit.Day => 30,
        ScheduleUnit.Week => 10,
        _ => 3,
    };

    /// <summary>軸の日数</summary>
    public int DayCount => End.DayNumber - Start.DayNumber + 1;

    /// <summary>軸の幅</summary>
    public double Width => DayCount * DayWidth;

    /// <summary>実績の時間を、日ごとに数字で出すか (日の単位だけ。週・月は幅が足りない)</summary>
    public bool ShowsHours => Unit == ScheduleUnit.Day;

    /// <summary>曜日・祝日の背景を出すか (月の単位は、1 日が細すぎて見分けられないので出さない)</summary>
    public bool ShowsOffDays => Unit != ScheduleUnit.Month;

    /// <summary>日付の列の、左端の位置</summary>
    /// <param name="date">日付</param>
    /// <returns>軸の左端からの距離</returns>
    public double X(DateOnly date) => (date.DayNumber - Start.DayNumber) * DayWidth;

    /// <summary>位置にある日付を返す</summary>
    /// <param name="x">軸の左端からの距離</param>
    /// <returns>その位置の日付 (軸の外は、端の日付)</returns>
    public DateOnly DateAt(double x)
    {
        var index = (int)Math.Floor(x / DayWidth);
        return Start.AddDays(Math.Clamp(index, 0, DayCount - 1));
    }

    /// <summary>月曜を 0 とした、曜日の番号</summary>
    /// <param name="date">日付</param>
    /// <returns>0 (月) から 6 (日)</returns>
    private static int MondayIndex(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;
}
