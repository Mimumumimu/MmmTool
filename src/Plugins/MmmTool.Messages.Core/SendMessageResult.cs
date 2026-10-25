namespace MmmTool.Messages.Core;

/// <summary>メッセージを送ろうとした結果の区分</summary>
public enum SendMessageOutcome
{
    /// <summary>送れた。メッセージと通知の状態を書いた</summary>
    Sent = 0,

    /// <summary>相手が受け取り先を指定していない。何も書かなかった</summary>
    NoReceiveTarget = 1,

    /// <summary>自分が参加していない会話、または相手の人が無い。何も書かなかった</summary>
    NotMember = 2,

    /// <summary>返信元が、同じ会話のメッセージではない。何も書かなかった</summary>
    ReplyInvalid = 3,

    /// <summary>送る人が、自分の受け取り先を指定していない。何も書かなかった</summary>
    SelfNoReceiveTarget = 4,
}

/// <summary>
/// メッセージを送った結果。
/// </summary>
/// <param name="Outcome">結果の区分</param>
/// <param name="ConversationId">書いた会話の番号。<see cref="SendMessageOutcome.Sent"/> のときだけ意味がある</param>
/// <param name="MessageId">書いたメッセージの番号。<see cref="SendMessageOutcome.Sent"/> のときだけ意味がある</param>
public sealed record SendMessageResult(SendMessageOutcome Outcome, int ConversationId, int MessageId)
{
    /// <summary>送れなかった結果を作る</summary>
    /// <param name="outcome">結果の区分</param>
    /// <returns>何も書かなかった結果</returns>
    public static SendMessageResult Failed(SendMessageOutcome outcome) => new(outcome, 0, 0);
}
