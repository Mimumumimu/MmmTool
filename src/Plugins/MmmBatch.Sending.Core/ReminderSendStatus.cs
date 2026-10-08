namespace MmmBatch.Sending.Core;

/// <summary>
/// リマインダーの送信の状況 1 件 (リマインダー × 送信先 × 日)。
/// </summary>
/// <param name="Id">番号 (<c>dbo.ReminderSendStatus.Id</c>)</param>
/// <param name="ReminderId">送るリマインダーの番号</param>
/// <param name="ChannelId">送る先の番号</param>
/// <param name="Date">どの日の分か</param>
/// <param name="Status">状態</param>
/// <param name="Attempts">送ろうとした回数 (送る前に数える)</param>
/// <param name="LastError">最後の失敗の理由。無ければ空文字</param>
/// <param name="SentAt">送った日時。まだ送っていなければ null</param>
/// <param name="UpdatedAt">最後に更新した日時 (DB の時計)。送り直しの権利を取るときの、読んだ内容の目印にもなる</param>
/// <remarks>ログではなく状態で、「見て、送っていなければ送る」判断に使う。</remarks>
public sealed record ReminderSendStatus(
    int Id, int ReminderId, int ChannelId, DateOnly Date, SendStatus Status, int Attempts, string LastError, DateTimeOffset? SentAt, DateTimeOffset UpdatedAt);
