namespace MmmTool.Reminders.Core;

/// <summary>
/// まとめて追加で選んだ、会議 1 件の下書き。
/// </summary>
/// <param name="StartTime">会議の開始時刻</param>
/// <param name="Title">件名。空のときは <see cref="DefaultTitle"/> として扱う</param>
/// <param name="Link">リンク。空のときは付けない</param>
/// <remarks>
/// 鳴らす時刻と、開始時刻を足した件名は、何分前に鳴らすかによって決まるので、<see cref="Resolve"/> で求める。
/// 画面に依存しないので、ほかの画面からも使える。
/// </remarks>
public sealed record ReminderDraft(TimeOnly StartTime, string? Title, string? Link)
{
    /// <summary>件名が空のときの件名</summary>
    public const string DefaultTitle = "会議";

    /// <summary>開始時刻を件名の後ろに足す文字の最大文字数 (空白 + <c>(HH:mm 開始)</c>)</summary>
    private const int StartSuffixMaxLength = 12;

    /// <summary>件名の最大文字数</summary>
    /// <remarks>開始時刻を足しても <see cref="ReminderLimits.TitleMaxLength"/> に収まる長さ。</remarks>
    public const int TitleMaxLength = ReminderLimits.TitleMaxLength - StartSuffixMaxLength;

    /// <summary>何分前に鳴らすかを反映した、鳴らす時刻と件名を求める</summary>
    /// <param name="leadMinutes">何分前に鳴らすか。0 は開始時刻ちょうど</param>
    /// <returns>鳴らす時刻と件名</returns>
    /// <remarks>
    /// 0 分前のときは、開始時刻と件名をそのまま返す。
    /// 前に鳴らすときは、時刻を前へずらし、件名の後ろに開始時刻を足す。足した件名の例は <c>定例会議 (10:00 開始)</c> となる。
    /// 0 時より前になるときは、前の日には回さず 0 時にする。
    /// </remarks>
    public (TimeOnly Time, string Title) Resolve(int leadMinutes)
    {
        var title = string.IsNullOrWhiteSpace(Title) ? DefaultTitle : Title.Trim();
        if (leadMinutes <= 0)
        {
            return (StartTime, title);
        }

        var minutes = Math.Max(0, StartTime.Hour * 60 + StartTime.Minute - leadMinutes);
        var time = new TimeOnly(minutes / 60, minutes % 60);
        return (time, $"{title} ({StartTime:HH\\:mm} 開始)");
    }

    /// <summary>リマインダーにする</summary>
    /// <param name="date">発動日</param>
    /// <param name="leadMinutes">何分前に鳴らすか。0 は開始時刻ちょうど</param>
    /// <param name="targetUserId">宛先のユーザーの <c>AppUser.Id</c>。ローカルモードは 0</param>
    /// <param name="isSpeak">通知のときに読み上げるか</param>
    /// <returns>日付指定で、未採番 (<see cref="Reminder.No"/> が 0)のリマインダー</returns>
    public Reminder ToReminder(DateOnly date, int leadMinutes, int targetUserId, bool isSpeak)
    {
        var (time, title) = Resolve(leadMinutes);
        return new Reminder
        {
            Date = ReminderDates.ToDateValue(date),
            Time = ReminderDates.ToTimeValue(time),
            Title = title,
            Link = string.IsNullOrWhiteSpace(Link) ? null : Link.Trim(),
            IsSpeak = isSpeak,
            TargetUserId = targetUserId,
        };
    }
}
