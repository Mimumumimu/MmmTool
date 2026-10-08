using MmmTool.Reminders.Core;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 送信の状況の一覧の 1 行 (状況に、リマインダーの件名と、送信先の情報を添えたもの)。
/// </summary>
/// <param name="Status">送信の状況</param>
/// <param name="Title">リマインダーの件名。リマインダーが無ければ空文字</param>
/// <param name="ChannelKind">送信先の区分。送信先が無ければ null</param>
/// <param name="ChannelName">送信先の登録名。送信先が無ければ空文字</param>
/// <param name="OwnerName">送信先を登録した人の表示名。分からなければ空文字</param>
/// <remarks>送る先の値 (トピック名・Webhook の URL)は秘密なので、持たない。</remarks>
public sealed record SendHistoryItem(ReminderSendStatus Status, string Title, NotificationChannelKind? ChannelKind, string ChannelName, string OwnerName);
