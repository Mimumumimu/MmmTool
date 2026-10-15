namespace MmmTool.Messages.Core;

/// <summary>
/// メッセージの送信依頼 1 件。
/// </summary>
/// <param name="Id">番号 (<c>dbo.MessageSendRequest.Id</c>)</param>
/// <param name="SenderUserId">送った人 (<c>AppUser.Id</c>)</param>
/// <param name="ChannelId">送る先 (<c>NotificationChannel.Id</c>)。送信先の値は持たない</param>
/// <param name="Title">タイトル。空文字ならタイトルなし</param>
/// <param name="Body">本文</param>
/// <param name="Status">状態</param>
/// <param name="Attempts">送ろうとした回数 (送る前に数える)</param>
/// <param name="CreatedAt">依頼した日時 (DB の時計)。画面に時刻として出すだけで、条件には使わない</param>
/// <param name="UpdatedAt">最後に更新した日時 (DB の時計)。送り直しの権利を取るときの、読んだ内容の目印にもなる</param>
/// <remarks>MmmTool は足すだけで、状態を変えない。送るのと、状態を変えるのは MmmBatch。</remarks>
public sealed record MessageSendRequest(
    int Id, int SenderUserId, int ChannelId, string Title, string Body, MessageSendStatus Status, int Attempts, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
