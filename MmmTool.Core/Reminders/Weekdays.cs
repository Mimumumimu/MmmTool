namespace MmmTool.Core.Reminders;

/// <summary>曜日フラグ (月〜日のビット OR)</summary>
[Flags]
public enum Weekdays
{
    /// <summary>指定なし (日付指定)</summary>
    None = 0,
    /// <summary>月曜日</summary>
    Monday = 1,
    /// <summary>火曜日</summary>
    Tuesday = 2,
    /// <summary>水曜日</summary>
    Wednesday = 4,
    /// <summary>木曜日</summary>
    Thursday = 8,
    /// <summary>金曜日</summary>
    Friday = 16,
    /// <summary>土曜日</summary>
    Saturday = 32,
    /// <summary>日曜日</summary>
    Sunday = 64,
}
