namespace MmmTool.Messages.Core;

/// <summary>
/// 会話の中の 1 通。
/// </summary>
/// <param name="Id">番号 <c>dbo.Message.Id</c>。会話の中の並びも、この番号で決める</param>
/// <param name="ConversationId">会話の番号</param>
/// <param name="SenderUserId">送った人 <c>AppUser.Id</c>。0 はシステムメッセージ</param>
/// <param name="SenderName">送った人の表示名。分からなければ空文字</param>
/// <param name="ReplyToMessageId">返信元のメッセージの番号。返信でなければ 0</param>
/// <param name="ReplyToSenderName">返信元を送った人の表示名。返信でない、または読めないときは null</param>
/// <param name="ReplyToText">返信元の本文。返信でない、または読めないときは null</param>
/// <param name="Body">本文。システムメッセージは、出来上がった文</param>
/// <param name="CreatedAt">送った日時。DB の時計で、画面に時刻として出すだけで、条件には使わない</param>
/// <param name="UpdatedAt">最後に更新した日時。DB の時計で、端末が差分を取る基準になる</param>
/// <remarks>送ったメッセージは、書いた時点で、会話に入っている人が読める。通知が届いたかは、画面に出さない。</remarks>
public sealed record ChatMessage(
    int Id, int ConversationId, int SenderUserId, string SenderName, int ReplyToMessageId, string? ReplyToSenderName, string? ReplyToText,
    string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    /// <summary>システムメッセージ (人の追加・抜けた・名前の変更)か</summary>
    public bool IsSystem => SenderUserId == SystemMessage.SenderUserId;
}
