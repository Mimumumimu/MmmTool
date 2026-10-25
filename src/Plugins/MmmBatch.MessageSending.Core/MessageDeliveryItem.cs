using MmmTool.Messages.Core;

namespace MmmBatch.MessageSending.Core;

/// <summary>
/// メッセージの通知の状態 1 件。1 通を、1 人に通知する状態 (<c>dbo.MessageDelivery</c>)と、そのメッセージの本文。
/// </summary>
/// <param name="Id">通知の状態の番号 <c>dbo.MessageDelivery.Id</c></param>
/// <param name="MessageId">メッセージの番号 <c>dbo.Message.Id</c></param>
/// <param name="RecipientUserId">通知を受ける人 <c>AppUser.Id</c></param>
/// <param name="SenderUserId">メッセージを送った人 <c>AppUser.Id</c></param>
/// <param name="Body">本文</param>
/// <param name="Status">状態</param>
/// <param name="Attempts">送ろうとした回数。送る前に数える</param>
/// <param name="CreatedAt">通知を頼んだ日時。DB の時計で、画面に時刻として出すだけで、条件には使わない</param>
/// <param name="UpdatedAt">最後に更新した日時。DB の時計で、送る権利を取るときの、読んだ内容の目印にもなる</param>
/// <remarks>MmmTool と Web 版は足すだけで、状態を変えない。送るのと、状態を変えるのは MmmBatch。</remarks>
public sealed record MessageDeliveryItem(
    int Id, int MessageId, int RecipientUserId, int SenderUserId, string Body, MessageSendStatus Status, int Attempts, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
