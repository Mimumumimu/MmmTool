using MmmBatch.Sending.Core;
using MmmTool.NotificationChannels.Core;
using MmmTool.Reminders.Core;

namespace MmmBatch.ReminderSending.Core;

/// <summary>リマインダーの送信の送る内容と送信のログ・画面での機能の印</summary>
public static class ReminderSendContent
{
    /// <summary>通知の見出し</summary>
    /// <remarks>どの送信先でも同じ</remarks>
    public const string Title = ReminderNotificationText.Heading;

    /// <summary>送信のログの機能のキーで、SendLogEntry.Source の値</summary>
    /// <remarks>古いログの行が持たないときの値でもあるので、変えない。</remarks>
    public const string Source = SendLogEntry.LegacySource;

    /// <summary>Web 版の提供元のキー</summary>
    /// <remarks>
    /// 通知の識別子の先頭になる
    /// Web 版の SqlServerReceivedReminderSource と合わせる
    /// </remarks>
    public const string ItemKind = "reminder";

    /// <summary>リマインダーから送る内容を作る</summary>
    /// <param name="reminder">送るリマインダー</param>
    /// <param name="statusId">
    /// この送信の状況の行の番号 <c>ReminderSendStatus.Id</c>
    /// Web 版が受け取った通知として一覧に出す行
    /// 状況を作らないテスト送信は null
    /// </param>
    /// <returns>
    /// 送る内容。
    /// どの送信先でも、見出しは「リマインダー」で、本文は 1 行目が件名、備考があれば次の行。
    /// リンクがあれば付ける。
    /// Discord では見出しが太字の 1 行目になる。
    /// </returns>
    public static SendMessage FromReminder(Reminder reminder, int? statusId = null)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        var body = ReminderNotificationText.Body(reminder.Title, reminder.Note);
        var link = string.IsNullOrWhiteSpace(reminder.Link) ? null : reminder.Link.Trim();
        var itemKey = statusId is { } id ? $"{ItemKind}:{id}" : null;
        return new SendMessage(Title, body, link, TitleInContent: true, WebPush: new WebPushContent(Title, body, itemKey));
    }
}
