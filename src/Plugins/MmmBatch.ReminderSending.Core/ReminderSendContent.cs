using MmmBatch.Sending.Core;
using MmmTool.Reminders.Core;

namespace MmmBatch.ReminderSending.Core;

/// <summary>リマインダーの送信の、送る内容と、送信のログ・画面での機能の印</summary>
public static class ReminderSendContent
{
    /// <summary>通知のタイトル</summary>
    public const string Title = "リマインダー";

    /// <summary>送信のログの機能のキー (<see cref="SendLogEntry.Source"/>)。古いログの行が持たないときの値でもあるので、変えない</summary>
    public const string Source = SendLogEntry.LegacySource;

    /// <summary>リマインダーから、送る内容を作る</summary>
    /// <param name="reminder">送るリマインダー</param>
    /// <returns>送る内容。本文は件名 (備考があれば「件名 (備考)」)、リンクがあればリンク</returns>
    public static SendMessage FromReminder(Reminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        var body = string.IsNullOrWhiteSpace(reminder.Note) ? reminder.Title : $"{reminder.Title} ({reminder.Note})";
        var link = string.IsNullOrWhiteSpace(reminder.Link) ? null : reminder.Link.Trim();
        return new SendMessage(Title, body, link);
    }
}
