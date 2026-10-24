namespace MmmTool.Messages.Core;

/// <summary>
/// 会話の中の 1 通。
/// </summary>
/// <param name="Id">番号 <c>dbo.Message.Id</c>。会話の中の並びも、この番号で決める</param>
/// <param name="ConversationId">会話の番号</param>
/// <param name="SenderUserId">送った人 <c>AppUser.Id</c></param>
/// <param name="SenderName">送った人の表示名。分からなければ空文字</param>
/// <param name="ReplyToMessageId">返信元のメッセージの番号。返信でなければ 0</param>
/// <param name="ReplyToSenderName">返信元を送った人の表示名。返信でない、または読めないときは null</param>
/// <param name="ReplyToText">返信元の本文。返信でない、または読めないときは null</param>
/// <param name="Body">本文</param>
/// <param name="CreatedAt">送った日時。DB の時計で、画面に時刻として出すだけで、条件には使わない</param>
/// <param name="UpdatedAt">最後に更新した日時。DB の時計で、端末が差分を取る基準になる</param>
/// <param name="DeliveryStatus">自分が送ったメッセージの、通知の状態。送信待ち・失敗のとき、送った人の画面に印を付ける。自分が送ったメッセージでなければ null</param>
/// <remarks>送ったメッセージは、書いた時点で、会話に入っている人が読める。<paramref name="DeliveryStatus"/> は、通知が届いたかを表すだけで、読めるかには関係しない。</remarks>
public sealed record ChatMessage(
    int Id, int ConversationId, int SenderUserId, string SenderName, int ReplyToMessageId, string? ReplyToSenderName, string? ReplyToText,
    string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, MessageSendStatus? DeliveryStatus);
