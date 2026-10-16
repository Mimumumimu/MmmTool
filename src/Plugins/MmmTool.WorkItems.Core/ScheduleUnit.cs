namespace MmmTool.WorkItems.Core;

/// <summary>スケジュールの日付の軸の単位 (1 日の幅と、見出しの出し方が変わる)</summary>
public enum ScheduleUnit
{
    /// <summary>日 (既定。1 日ごとの列。実績の時間を数字で出す)</summary>
    Day = 0,

    /// <summary>週 (1 日の幅を狭くして、月曜の日付を見出しに出す)</summary>
    Week = 1,

    /// <summary>月 (1 日の幅をさらに狭くして、月を見出しに出す)</summary>
    Month = 2,
}
