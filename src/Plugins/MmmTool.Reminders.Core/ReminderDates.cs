using MmmSdk.Core.Components.DaysOfWeek;

namespace MmmTool.Reminders.Core;

/// <summary>
/// リマインダーの日付・時刻の変換と、発動日の判定。
/// </summary>
/// <remarks>日付は yyyyMMdd、時刻は HHmm の整数で保存する。曜日の変換と表記は SDK の <see cref="WeekdaySet"/>。</remarks>
public static class ReminderDates
{
    /// <summary>日付なし (曜日指定)を表す特殊値</summary>
    public const int NoDate = 99999999;

    /// <summary>曜日指定 (日付なし)か</summary>
    /// <param name="date">発動日 (yyyyMMdd の整数)</param>
    /// <returns>曜日指定なら true</returns>
    public static bool IsWeekdaySpecified(int date) => date == NoDate;

    /// <summary>日付を yyyyMMdd の整数にする</summary>
    /// <param name="date">日付</param>
    /// <returns>yyyyMMdd の整数</returns>
    public static int ToDateValue(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;

    /// <summary>日時の日付部分を yyyyMMdd の整数にする</summary>
    /// <param name="dateTime">日時</param>
    /// <returns>yyyyMMdd の整数</returns>
    public static int ToDateValue(DateTime dateTime) => ToDateValue(DateOnly.FromDateTime(dateTime));

    /// <summary>yyyyMMdd の整数を日付にする</summary>
    /// <param name="value">yyyyMMdd の整数</param>
    /// <returns>日付なし (<see cref="NoDate"/>)・日付として正しくない値なら null。</returns>
    public static DateOnly? ToDate(int value)
    {
        if (value == NoDate)
        {
            return null;
        }

        var (year, month, day) = (value / 10000, value / 100 % 100, value % 100);
        if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }
        return new DateOnly(year, month, day);
    }

    /// <summary>時刻を HHmm の整数にする</summary>
    /// <param name="time">時刻</param>
    /// <returns>HHmm の整数</returns>
    public static int ToTimeValue(TimeOnly time) => time.Hour * 100 + time.Minute;

    /// <summary>日時の時刻部分を HHmm の整数にする (秒以下は切り捨て)</summary>
    /// <param name="dateTime">日時</param>
    /// <returns>HHmm の整数</returns>
    public static int ToTimeValue(DateTime dateTime) => ToTimeValue(TimeOnly.FromDateTime(dateTime));

    /// <summary>HHmm の整数を時刻にする</summary>
    /// <param name="value">HHmm の整数</param>
    /// <returns>時刻として正しくない値なら null。</returns>
    public static TimeOnly? ToTime(int value)
    {
        var (hour, minute) = (value / 100, value % 100);
        if (value < 0 || hour > 23 || minute > 59)
        {
            return null;
        }
        return new TimeOnly(hour, minute);
    }

    /// <summary>リマインダーが指定の日に発動するか</summary>
    /// <param name="reminder">リマインダー</param>
    /// <param name="date">調べる日</param>
    /// <returns>発動するなら true</returns>
    /// <remarks>
    /// 発動日がその日、または曜日指定でその日の曜日を含む。
    /// 曜日を 1 つも選んでいないときと、7 つ全部のときは毎日。判定は <see cref="WeekdaySet.OccursOn"/>。
    /// </remarks>
    public static bool OccursOn(Reminder reminder, DateOnly date)
        => reminder.Date == ToDateValue(date)
            || (IsWeekdaySpecified(reminder.Date) && WeekdaySet.OccursOn(reminder.Weekdays, date.DayOfWeek));
}
