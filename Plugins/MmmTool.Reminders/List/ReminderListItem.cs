using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.List;

/// <summary>リマインダー一覧の 1 行</summary>
/// <param name="source">元のリマインダー</param>
/// <remarks>表示用の文字列を持つ。一覧は変更があるたびに作り直すので、値は変わらない。</remarks>
public sealed class ReminderListItem(Reminder source)
{
    /// <summary>該当なしの表示</summary>
    private const string None = "－";

    /// <summary>元のリマインダー</summary>
    public Reminder Source { get; } = source;

    /// <summary>日付 (日付指定は yyyy/MM/dd、曜日指定は「－」)</summary>
    public string DateText { get; } = ReminderDates.ToDate(source.Date) is { } date ? date.ToString("yyyy/MM/dd") : None;

    /// <summary>曜日 (「月火水」形式。曜日を選んでいない曜日指定は「毎日」、日付指定は「－」)</summary>
    public string WeekdayText { get; } = ReminderDates.IsWeekdaySpecified(source.Date)
        ? ReminderDates.DescribeWeekdays(source.Weekdays)
        : None;

    /// <summary>時刻 (HH:mm)</summary>
    public string TimeText { get; } = ReminderDates.ToTime(source.Time) is { } time ? time.ToString("HH:mm") : None;

    /// <summary>件名</summary>
    public string Title => Source.Title;

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted => Source.IsDeleted;
}
