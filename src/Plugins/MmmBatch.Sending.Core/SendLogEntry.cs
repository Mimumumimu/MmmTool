using MmmTool.Reminders.Core;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 送信のログの 1 行 (送った 1 回の結果)。
/// </summary>
/// <param name="At">送った日時 (結果が決まった日時)</param>
/// <param name="ReminderId">送ったリマインダーの番号</param>
/// <param name="Title">リマインダーの件名 (送った時点のもの)</param>
/// <param name="OwnerName">送信先を登録した人の表示名 (送った時点のもの)</param>
/// <param name="ChannelId">送った先の番号</param>
/// <param name="ChannelKind">送信先の区分</param>
/// <param name="ChannelName">送信先の登録名 (送った時点のもの)</param>
/// <param name="Attempt">その日の、その送信先への、何回目の送信か</param>
/// <param name="Status">結果 (<see cref="SendStatus.Sent"/> か <see cref="SendStatus.Failed"/>)</param>
/// <param name="Error">失敗の理由。成功は空文字</param>
/// <remarks>
/// 件名・名前を、送った時点のまま持つので、あとでリマインダーや送信先を直したり消したりしても、ログは変わらない。
/// 送る先の値 (トピック名・Webhook の URL)は秘密なので、持たない。
/// </remarks>
public sealed record SendLogEntry(
    DateTimeOffset At, int ReminderId, string Title, string OwnerName, int ChannelId, NotificationChannelKind ChannelKind, string ChannelName,
    int Attempt, SendStatus Status, string Error);
