using MmmSdk.Core.Components.DaysOfWeek;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.List;

/// <summary>リマインダー一覧の 1 行</summary>
/// <param name="source">元のリマインダー</param>
/// <param name="canPurge">完全削除ができる保存先か</param>
/// <remarks>
/// 表示用の文字列を持つ。
/// 一覧は変更があるたびに作り直すので、値は変わらない。
/// </remarks>
public sealed class ReminderListItem(Reminder source, bool canPurge)
{
    /// <summary>該当なしの表示</summary>
    private const string None = "－";

    /// <summary>元のリマインダー</summary>
    public Reminder Source { get; } = source;

    /// <summary>日付</summary>
    /// <remarks>日付指定は yyyy/MM/dd、曜日指定は「－」。</remarks>
    public string DateText { get; } = ReminderDates.ToDate(source.Date) is { } date ? date.ToString("yyyy/MM/dd") : None;

    /// <summary>曜日の表示</summary>
    /// <remarks>
    /// 「月火水」の形。
    /// 曜日を選んでいない曜日指定と 7 つ全部は「毎日」で、日付指定は「－」。
    /// </remarks>
    public string WeekdayText { get; } = ReminderDates.IsWeekdaySpecified(source.Date)
        ? WeekdaySet.Describe(source.Weekdays)
        : None;

    /// <summary>時刻</summary>
    /// <remarks>HH:mm</remarks>
    public string TimeText { get; } = ReminderDates.ToTime(source.Time) is { } time ? time.ToString("HH:mm") : None;

    /// <summary>件名</summary>
    public string Title => Source.Title;

    /// <summary>備考</summary>
    /// <remarks>
    /// 前後の空白を除く
    /// 備考が無ければ空文字列
    /// </remarks>
    public string Note { get; } = source.Note?.Trim() ?? "";

    /// <summary>一覧の列に出す備考</summary>
    /// <remarks>
    /// 改行を空白にして 1 行にしたもの
    /// 複数行のまま出すと行の高さが行ごとに変わるため
    /// 全文はカーソルを合わせたツールチップで見せる
    /// </remarks>
    public string NoteLine { get; } = string.Join(' ', (source.Note ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>備考があるか</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted => Source.IsDeleted;

    /// <summary>メニューに「削除」を出すか</summary>
    /// <remarks>削除済みでないときで、見えるリマインダーは誰でも削除できる。</remarks>
    public bool CanDelete { get; } = !source.IsDeleted;

    /// <summary>メニューに「完全削除」を出すか</summary>
    /// <remarks>削除済みで、保存先が完全削除に対応している。</remarks>
    public bool CanPurge { get; } = canPurge && source.IsDeleted;
}
