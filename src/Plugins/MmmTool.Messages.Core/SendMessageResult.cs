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

    /// <summary>新しいグループの名前か参加者が正しくない。何も書かなかった</summary>
    /// <remarks>名前が空か長すぎる・自分のほかに参加者がいない・参加者が自分を含めて 50 人を超える・参加者に使えない人がいる。</remarks>
    GroupInvalid = 5,
}

/// <summary>グループの参加者や名前を変えようとした結果の区分</summary>
public enum GroupChangeOutcome
{
    /// <summary>変えた。システムメッセージを書いた</summary>
    Done = 0,

    /// <summary>自分が参加していない会話、または会話が無い。何も書かなかった</summary>
    NotMember = 1,

    /// <summary>グループではない 1 対 1 の会話。何も書かなかった</summary>
    NotGroup = 2,

    /// <summary>名前が正しくない・人数が 50 人を超える・追加する人に使えない人がいる。何も書かなかった</summary>
    Invalid = 3,
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
