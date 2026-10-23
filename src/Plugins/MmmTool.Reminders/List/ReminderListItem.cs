using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.List;

/// <summary>リマインダー一覧の 1 行</summary>
/// <param name="source">元のリマインダー</param>
/// <param name="canPurge">完全削除ができる保存先か</param>
/// <remarks>表示用の文字列を持つ。一覧は変更があるたびに作り直すので、値は変わらない。</remarks>
public sealed class ReminderListItem(Reminder source, bool canPurge)
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

    /// <summary>備考</summary>
    /// <remarks>前後の空白を除く。備考が無ければ空文字列。</remarks>
    public string Note { get; } = source.Note?.Trim() ?? "";

    /// <summary>一覧の列に出す備考 (改行を空白にして 1 行にしたもの)</summary>
    /// <remarks>複数行のまま出すと行の高さが行ごとに変わるため。全文は、カーソルを合わせたツールチップで見せる。</remarks>
    public string NoteLine { get; } = string.Join(' ', (source.Note ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>備考があるか</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted => Source.IsDeleted;

    /// <summary>メニューに「削除」を出すか (削除済みでない。見えるリマインダーは、誰でも削除できる)</summary>
    public bool CanDelete { get; } = !source.IsDeleted;

    /// <summary>メニューに「完全削除」を出すか (削除済みで、保存先が完全削除に対応している)</summary>
    public bool CanPurge { get; } = canPurge && source.IsDeleted;
}
