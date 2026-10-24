using MmmBatch.MessageSending.Core;
using MmmTool.Messages.Core;

namespace MmmBatch.MessageSending;

/// <summary>メッセージの送信の画面に出す文字列。最近の依頼の状態</summary>
public static class MessageSendDisplay
{
    /// <summary>メッセージの通知の状態を、画面に出す文字列にする</summary>
    /// <param name="request">通知の状態</param>
    /// <returns>まだ送ろうとしていないもの、つまり送信待ちで回数が 0 のものは「未送信」。ほかは「送信中」「送信済み」「失敗」。失敗の理由は出さない。「履歴」で見る</returns>
    public static string StatusText(MessageDeliveryItem request) => request switch
    {
        { Status: MessageSendStatus.Pending, Attempts: 0 } => "未送信",
        { Status: MessageSendStatus.Pending } => "送信中",
        { Status: MessageSendStatus.Sent } => "送信済み",
        { Status: MessageSendStatus.Failed } => "失敗",
        _ => request.Status.ToString(),
    };
}
