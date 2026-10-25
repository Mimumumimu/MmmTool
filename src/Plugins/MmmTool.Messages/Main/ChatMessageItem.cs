using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>
/// 会話の中の 1 通。画面に出す文字と、並べる向き。
/// </summary>
/// <param name="source">メッセージ</param>
/// <param name="isMine">自分が送ったメッセージか</param>
/// <remarks>受け取ったメッセージは左、自分が送ったメッセージは右に寄せる。向きと、出す・出さないは、画面が、この値から決める。</remarks>
public sealed class ChatMessageItem(ChatMessage source, bool isMine)
{
    /// <summary>元のメッセージ</summary>
    public ChatMessage Source { get; } = source;

    /// <summary>メッセージの番号</summary>
    public int Id { get; } = source.Id;

    /// <summary>自分が送ったメッセージか</summary>
    public bool IsMine { get; } = isMine;

    /// <summary>送った人の表示名。自分が送ったメッセージは空文字</summary>
    public string SenderText { get; } = isMine ? "" : source.SenderName;

    /// <summary>本文</summary>
    public string Body { get; } = source.Body;

    /// <summary>送った日時の表示</summary>
    public string TimeText { get; } = MessageTimeText.Format(source.CreatedAt);

    /// <summary>返信元の引用。送った人の名前と本文の 1 行目。返信でない、または読めないときは空文字</summary>
    public string QuoteText { get; } = source is { ReplyToMessageId: > 0, ReplyToText: not null }
        ? $"{source.ReplyToSenderName}  {source.ReplyToText}"
        : "";

    /// <summary>通知の状態の印。自分が送ったメッセージの、送信待ちは「送信待ち」、失敗は「失敗」。それ以外は空文字</summary>
    public string MarkText { get; } = isMine
        ? source.DeliveryStatus switch
        {
            MessageSendStatus.Pending => "送信待ち",
            MessageSendStatus.Failed => "失敗",
            _ => "",
        }
        : "";

    /// <summary>返信元の引用を出すか</summary>
    public bool HasQuote => QuoteText.Length > 0;

    /// <summary>通知の状態の印を出すか</summary>
    public bool HasMark => MarkText.Length > 0;
}
