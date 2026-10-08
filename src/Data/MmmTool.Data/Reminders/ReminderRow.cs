using MmmTool.Reminders.Core;
using WeekdayFlags = MmmTool.Reminders.Core.Weekdays;

namespace MmmTool.Data.Reminders;

/// <summary>
/// DB の <c>dbo.Reminder</c> の 1 行 (列と 1 対 1)。アプリの型 <see cref="Reminder"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>
/// 日付なしは <see cref="DateOnly.MaxValue"/> (<c>9999-12-31</c>)、備考・リンクの空は空文字。NULL は使わない
/// (理由は docs/specs/database.md)。<see cref="Reminder"/> との変換は <see cref="FromReminder"/> と <see cref="ToReminder"/>。
/// </remarks>
public sealed record ReminderRow
{
    /// <summary>主キー (<see cref="Reminder.No"/>)。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    /// <remarks>DB の既定値で入る。<see cref="FromReminder"/> では決めない。</remarks>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (<c>AppUser.Id</c>)</summary>
    /// <remarks>アプリの型の <see cref="Reminder.CreatedByUserId"/> は、DB から読むだけ (<see cref="ToReminder"/>)。作成するときは、Repository が今のユーザーを入れ、更新では変えないので、<see cref="FromReminder"/> では決めない。</remarks>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    /// <remarks>更新の SQL の中で、DB サーバーの時計を使って書く。<see cref="FromReminder"/> では決めない。</remarks>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (<c>AppUser.Id</c>)</summary>
    /// <remarks>アプリの型に無いので、<see cref="FromReminder"/> では決めない (Repository が入れる)。</remarks>
    public int UpdatedByUserId { get; init; }

    /// <summary>宛先 (<c>AppUser.Id</c>)。0 は全員宛て</summary>
    /// <remarks>アプリの型の <see cref="Reminder.TargetUserId"/> と同じ。</remarks>
    public int TargetUserId { get; init; }

    /// <summary>日付指定の日付</summary>
    /// <remarks>曜日指定 (日付なし)は <see cref="DateOnly.MaxValue"/> (<c>9999-12-31</c>)。</remarks>
    public DateOnly Date { get; init; } = DateOnly.MaxValue;

    /// <summary>時刻 (秒は 0)</summary>
    public TimeOnly Time { get; init; }

    /// <summary>曜日指定の曜日 (月 = 1・火 = 2・水 = 4・木 = 8・金 = 16・土 = 32・日 = 64 を足した数。JSON と同じ)</summary>
    public byte Weekdays { get; init; }

    /// <summary>件名</summary>
    public string Title { get; init; } = "";

    /// <summary>備考 (無いときは空文字)</summary>
    public string Note { get; init; } = "";

    /// <summary>リンク (無いときは空文字)</summary>
    public string Link { get; init; } = "";

    /// <summary>通知のときに読み上げるか</summary>
    public bool IsSpeak { get; init; }

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="reminder">リマインダー</param>
    /// <returns>DB の行。作成・更新の日時と作成者・更新者は決めない (Repository が入れる)</returns>
    /// <exception cref="ArgumentException">日付・時刻・曜日が、DB に入れられない値 (手で直した JSON の誤りなど)。</exception>
    public static ReminderRow FromReminder(Reminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        var date = DateOnly.MaxValue;
        if (!ReminderDates.IsWeekdaySpecified(reminder.Date))
        {
            date = ReminderDates.ToDate(reminder.Date)
                ?? throw new ArgumentException($"日付 {reminder.Date} は、日付として正しくありません。", nameof(reminder));
            if (date == DateOnly.MaxValue)
            {
                throw new ArgumentException("9999-12-31 は、日付なしを表すので、日付指定には使えません。", nameof(reminder));
            }
        }

        var time = ReminderDates.ToTime(reminder.Time)
            ?? throw new ArgumentException($"時刻 {reminder.Time} は、時刻として正しくありません。", nameof(reminder));

        if ((int)reminder.Weekdays is < 0 or > 127)
        {
            throw new ArgumentException($"曜日 {(int)reminder.Weekdays} は、0 から 127 の範囲ではありません。", nameof(reminder));
        }

        if (reminder.TargetUserId < 0)
        {
            throw new ArgumentException($"宛先 {reminder.TargetUserId} は、負の数です。", nameof(reminder));
        }

        // 長すぎる文字列は、DB のパラメーターの長さで黙って切り詰められてしまうので、ここで止める (画面の上限は ReminderLimits)
        if (reminder.Title.Length > ReminderLimits.TitleMaxLength
            || (reminder.Note?.Length ?? 0) > ReminderLimits.NoteMaxLength
            || (reminder.Link?.Length ?? 0) > ReminderLimits.LinkMaxLength)
        {
            throw new ArgumentException(
                $"件名は {ReminderLimits.TitleMaxLength} 文字、備考は {ReminderLimits.NoteMaxLength} 文字、リンクは {ReminderLimits.LinkMaxLength} 文字までです。", nameof(reminder));
        }

        return new ReminderRow
        {
            Id = reminder.No,
            IsDeleted = reminder.IsDeleted,
            TargetUserId = reminder.TargetUserId,
            Date = date,
            Time = time,
            Weekdays = (byte)reminder.Weekdays,
            Title = reminder.Title,
            Note = reminder.Note ?? "",
            Link = reminder.Link ?? "",
            IsSpeak = reminder.IsSpeak,
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>リマインダー。備考・リンクの空文字は null にする。時刻の秒は切り捨てる</returns>
    public Reminder ToReminder() => new()
    {
        No = Id,
        IsDeleted = IsDeleted,
        TargetUserId = TargetUserId,
        CreatedByUserId = CreatedByUserId,
        Date = Date == DateOnly.MaxValue ? ReminderDates.NoDate : ReminderDates.ToDateValue(Date),
        Time = ReminderDates.ToTimeValue(Time),
        Weekdays = (WeekdayFlags)Weekdays,
        Title = Title,
        Note = Note.Length == 0 ? null : Note,
        Link = Link.Length == 0 ? null : Link,
        IsSpeak = IsSpeak,
    };
}
