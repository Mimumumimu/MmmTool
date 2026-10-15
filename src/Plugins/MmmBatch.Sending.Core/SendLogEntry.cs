using MmmTool.NotificationChannels.Core;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 送信のログの 1 行 (送った 1 回の結果)。
/// </summary>
/// <param name="At">送った日時 (結果が決まった日時)</param>
/// <param name="ReminderId">送ったものの番号。リマインダーは、リマインダーの番号。メッセージは、送信依頼の番号</param>
/// <param name="Title">件名 (送った時点のもの)。メッセージは、タイトル (無ければ空文字)</param>
/// <param name="OwnerName">送信先を登録した人の表示名 (送った時点のもの)</param>
/// <param name="ChannelId">送った先の番号</param>
/// <param name="ChannelKind">送信先の区分</param>
/// <param name="ChannelName">送信先の登録名 (送った時点のもの)</param>
/// <param name="Attempt">何回目の送信か。リマインダーは、その日の、その送信先への回数</param>
/// <param name="Status">結果 (<see cref="SendStatus.Sent"/> か <see cref="SendStatus.Failed"/>)</param>
/// <param name="Error">失敗の理由。成功は空文字。失敗の理由は、DB には残さず、ここにだけ残す</param>
/// <param name="Source">送った機能のキー (ファイルを読む人が、どの機能の行か見分けるための印)。各機能が自分の値を決める。この項目を持たない古い行は <see cref="LegacySource"/> のリマインダーの行</param>
/// <param name="SenderName">メッセージを依頼した人の表示名 (送った時点のもの)。リマインダーは空文字</param>
/// <remarks>
/// 件名・名前を、送った時点のまま持つので、あとでリマインダーや送信先を直したり消したりしても、ログは変わらない。
/// 送る先の値 (トピック名・Webhook の URL)は秘密なので、持たない。
/// </remarks>
public sealed record SendLogEntry(
    DateTimeOffset At, int ReminderId, string Title, string OwnerName, int ChannelId, NotificationChannelKind ChannelKind, string ChannelName,
    int Attempt, SendStatus Status, string Error, string Source = SendLogEntry.LegacySource, string SenderName = "")
{
    /// <summary>機能のキーを持たない古い行の、機能のキー (リマインダーの値と同じ。ログのファイルには、この文字列で残っている)</summary>
    public const string LegacySource = "Reminder";
}
