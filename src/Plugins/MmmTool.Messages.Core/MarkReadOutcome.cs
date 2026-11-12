namespace MmmTool.Messages.Core;

/// <summary>読んだ位置を書こうとした結果の区分</summary>
public enum MarkReadOutcome
{
    /// <summary>読んだ位置を進めた</summary>
    /// <remarks>ほかの参加者の更新日時も進めた</remarks>
    Advanced = 0,

    /// <summary>今の読んだ位置が渡された番号以上だった</summary>
    /// <remarks>何も書かなかった</remarks>
    Unchanged = 1,

    /// <summary>自分が参加していない会話、または会話が無い</summary>
    /// <remarks>何も書かなかった</remarks>
    NotMember = 2,

    /// <summary>番号がその会話のメッセージではない</summary>
    /// <remarks>何も書かなかった</remarks>
    MessageInvalid = 3,
}
