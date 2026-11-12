namespace MmmTool.WorkItems.Core;

/// <summary>スケジュールの日付の軸の単位</summary>
/// <remarks>1 日の幅と見出しの出し方が変わる</remarks>
public enum ScheduleUnit
{
    /// <summary>日</summary>
    /// <remarks>既定で、1 日ごとの列にし、実績の時間を数字で出す。</remarks>
    Day = 0,

    /// <summary>週</summary>
    /// <remarks>1 日の幅を狭くして、月曜の日付を見出しに出す。</remarks>
    Week = 1,

    /// <summary>月</summary>
    /// <remarks>1 日の幅をさらに狭くして、月を見出しに出す。</remarks>
    Month = 2,
}
