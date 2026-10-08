using MmmTool.Reminders.Core;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 送る内容。
/// </summary>
/// <param name="Title">タイトル</param>
/// <param name="Body">本文 (件名。備考があれば「件名 (備考)」)</param>
/// <param name="Link">リンク。無ければ null</param>
public sealed record SendMessage(string Title, string Body, string? Link)
{
    /// <summary>通知のタイトル</summary>
    public const string ReminderTitle = "リマインダー";

    /// <summary>リマインダーから、送る内容を作る</summary>
    /// <param name="reminder">送るリマインダー</param>
    /// <returns>送る内容</returns>
    public static SendMessage FromReminder(Reminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        var body = string.IsNullOrWhiteSpace(reminder.Note) ? reminder.Title : $"{reminder.Title} ({reminder.Note})";
        var link = string.IsNullOrWhiteSpace(reminder.Link) ? null : reminder.Link.Trim();
        return new SendMessage(ReminderTitle, body, link);
    }

    /// <summary>リンクが、通知から開ける URL (http か https)か</summary>
    /// <returns>開ける URL なら true。ファイル・フォルダーのパスなどは false</returns>
    public bool IsLinkUrl => Link is not null
        && Uri.TryCreate(Link, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
