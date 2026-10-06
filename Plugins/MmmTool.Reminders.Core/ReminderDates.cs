using System.Text;

namespace MmmTool.Reminders.Core;

/// <summary>
/// リマインダーの日付・時刻・曜日の変換。
/// </summary>
/// <remarks>日付は yyyyMMdd、時刻は HHmm の整数で保存する。</remarks>
public static class ReminderDates
{
    /// <summary>日付なし (曜日指定)を表す特殊値</summary>
    public const int NoDate = 99999999;

    /// <summary>曜日フラグと表示名を、月曜から順に並べたもの (日本語の表記順)</summary>
    public static IReadOnlyList<(Weekdays Flag, string Name)> WeekdayNames { get; } =
    [
        (Weekdays.Monday, "月"),
        (Weekdays.Tuesday, "火"),
        (Weekdays.Wednesday, "水"),
        (Weekdays.Thursday, "木"),
        (Weekdays.Friday, "金"),
        (Weekdays.Saturday, "土"),
        (Weekdays.Sunday, "日"),
    ];

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

    /// <summary>曜日を曜日フラグにする</summary>
    /// <param name="dayOfWeek">曜日</param>
    /// <returns>対応する曜日フラグ</returns>
    public static Weekdays ToWeekdays(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => Weekdays.Monday,
        DayOfWeek.Tuesday => Weekdays.Tuesday,
        DayOfWeek.Wednesday => Weekdays.Wednesday,
        DayOfWeek.Thursday => Weekdays.Thursday,
        DayOfWeek.Friday => Weekdays.Friday,
        DayOfWeek.Saturday => Weekdays.Saturday,
        DayOfWeek.Sunday => Weekdays.Sunday,
        _ => Weekdays.None,
    };

    /// <summary>曜日フラグに指定の曜日が含まれるか</summary>
    /// <param name="weekdays">曜日フラグ</param>
    /// <param name="dayOfWeek">調べる曜日</param>
    /// <returns>含まれていれば true</returns>
    public static bool Contains(Weekdays weekdays, DayOfWeek dayOfWeek) => (weekdays & ToWeekdays(dayOfWeek)) != 0;

    /// <summary>曜日指定のリマインダーが、指定の曜日に発動するか</summary>
    /// <param name="weekdays">リマインダーの曜日指定</param>
    /// <param name="dayOfWeek">調べる曜日</param>
    /// <returns>発動するなら true</returns>
    /// <remarks>曜日を 1 つも選んでいない (<see cref="Weekdays.None"/>)ときは毎日発動する。</remarks>
    public static bool OccursOn(Weekdays weekdays, DayOfWeek dayOfWeek) => weekdays == Weekdays.None || Contains(weekdays, dayOfWeek);

    /// <summary>リマインダーが指定の日に発動するか</summary>
    /// <param name="reminder">リマインダー</param>
    /// <param name="date">調べる日</param>
    /// <returns>発動するなら true</returns>
    /// <remarks>発動日がその日、または曜日指定でその日の曜日を含む (曜日を 1 つも選んでいなければ毎日)。</remarks>
    public static bool OccursOn(Reminder reminder, DateOnly date)
        => reminder.Date == ToDateValue(date)
            || (IsWeekdaySpecified(reminder.Date) && OccursOn(reminder.Weekdays, date.DayOfWeek));

    /// <summary>曜日フラグを「月火水」のような日本語の文字列にする</summary>
    /// <param name="weekdays">曜日フラグ</param>
    /// <returns>月曜から順に並べた文字列。指定なしなら空文字</returns>
    public static string ToJapanese(Weekdays weekdays)
    {
        var builder = new StringBuilder();
        foreach (var (flag, name) in WeekdayNames)
        {
            if ((weekdays & flag) != 0)
            {
                builder.Append(name);
            }
        }
        return builder.ToString();
    }

    /// <summary>曜日指定を、画面に出す文字列にする</summary>
    /// <param name="weekdays">曜日フラグ</param>
    /// <returns>「月火水」のような文字列。曜日を 1 つも選んでいなければ「毎日」</returns>
    /// <remarks>日付指定のリマインダーには使わない (呼び出し側で <see cref="IsWeekdaySpecified"/> を確かめる)。</remarks>
    public static string DescribeWeekdays(Weekdays weekdays) => weekdays == Weekdays.None ? "毎日" : ToJapanese(weekdays);
}
