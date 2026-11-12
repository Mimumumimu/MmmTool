namespace MmmTool.Messages.Core;

/// <summary>メッセージを送ろうとした結果の区分</summary>
public enum SendMessageOutcome
{
    /// <summary>送れた</summary>
    /// <remarks>メッセージと通知の状態を書いた</remarks>
    Sent = 0,

    /// <summary>自分が参加していない会話、または相手の人が無い</summary>
    /// <remarks>
    /// 何も書かなかった
    /// 管理者にメッセージを無効にされている人は無いものとして扱う
    /// </remarks>
    NotMember = 1,

    /// <summary>返信元が同じ会話のメッセージではない</summary>
    /// <remarks>何も書かなかった</remarks>
    ReplyInvalid = 2,

    /// <summary>新しいグループの名前か参加者が正しくない</summary>
    /// <remarks>
    /// 何も書かなかった
    /// 名前が空か長すぎる・自分のほかに参加者がいない・参加者が自分を含めて 50 人を超える・参加者に使えない人がいる
    /// </remarks>
    GroupInvalid = 3,
}

/// <summary>グループの参加者や名前を変えようとした結果の区分</summary>
public enum GroupChangeOutcome
{
    /// <summary>変えた</summary>
    /// <remarks>システムメッセージを書いた</remarks>
    Done = 0,

    /// <summary>自分が参加していない会話、または会話が無い</summary>
    /// <remarks>何も書かなかった</remarks>
    NotMember = 1,

    /// <summary>グループではない 1 対 1 の会話</summary>
    /// <remarks>何も書かなかった</remarks>
    NotGroup = 2,

    /// <summary>名前が正しくない・人数が 50 人を超える・追加する人に使えない人がいる</summary>
    /// <remarks>何も書かなかった</remarks>
    Invalid = 3,
}

/// <summary>メッセージを送った結果</summary>
/// <param name="Outcome">結果の区分</param>
/// <param name="ConversationId">
/// 書いた会話の番号
/// <see cref="SendMessageOutcome.Sent"/> のときだけ意味がある
/// </param>
/// <param name="MessageId">
/// 書いたメッセージの番号
/// <see cref="SendMessageOutcome.Sent"/> のときだけ意味がある
/// </param>
public sealed record SendMessageResult(SendMessageOutcome Outcome, int ConversationId, int MessageId)
{
    /// <summary>送れなかった結果を作る</summary>
    /// <param name="outcome">結果の区分</param>
    /// <returns>何も書かなかった結果</returns>
    public static SendMessageResult Failed(SendMessageOutcome outcome) => new(outcome, 0, 0);
}
