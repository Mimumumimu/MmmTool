using MmmTool.NotificationChannels.Core;

namespace MmmBatch.Sending.Core;

/// <summary>送信のログの 1 行で送った 1 回の結果</summary>
/// <param name="At">送った日時 (結果が決まった日時)</param>
/// <param name="SourceId">
/// 送ったものの番号。
/// 機能ごとに、送ったものを指す番号を入れる。
/// </param>
/// <param name="Title">
/// 件名で、送った時点のもの。
/// メッセージはタイトル (無ければ空文字)。
/// </param>
/// <param name="OwnerName">送信先を登録した人の表示名 (送った時点のもの)</param>
/// <param name="ChannelId">送った先の番号</param>
/// <param name="ChannelKind">送信先の区分</param>
/// <param name="ChannelName">送信先の登録名 (送った時点のもの)</param>
/// <param name="Attempt">
/// 何回目の送信か。
/// リマインダーはその日の、その送信先への回数。
/// </param>
/// <param name="Status">結果 (<see cref="SendStatus.Sent"/> か <see cref="SendStatus.Failed"/>)</param>
/// <param name="Error">
/// 失敗の理由。
/// 成功は空文字。
/// 失敗の理由は DB には残さず、ここにだけ残す。
/// </param>
/// <param name="Source">
/// 送った機能のキーで、ファイルを読む人がどの機能の行か見分けるための印。
/// 各機能が自分の値を決める。
/// この項目を持たない古い行は <see cref="LegacySource"/> のリマインダーの行。
/// </param>
/// <param name="ActorName">
/// 送るきっかけを作った人の表示名で、送った時点のもの。
/// 機能が決める。
/// 該当する人がいなければ空文字。
/// </param>
/// <remarks>
/// 件名・名前を、送った時点のまま持つので、あとでリマインダーや送信先を直したり消したりしても、ログは変わらない。
/// 送る先の値 (トピック名・Webhook の URL)は秘密なので、持たない。
/// </remarks>
public sealed record SendLogEntry(
    DateTimeOffset At, int SourceId, string Title, string OwnerName, int ChannelId, NotificationChannelKind ChannelKind, string ChannelName,
    int Attempt, SendStatus Status, string Error, string Source = SendLogEntry.LegacySource, string ActorName = "")
{
    /// <summary>機能のキーを持たない古い行の機能のキーで、リマインダーの値と同じ</summary>
    /// <remarks>ログのファイルにはこの文字列で残っている</remarks>
    public const string LegacySource = "Reminder";
}
