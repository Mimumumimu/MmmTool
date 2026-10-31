namespace MmmTool.Messages.Core;

/// <summary>読んだ位置を書こうとした結果の区分</summary>
public enum MarkReadOutcome
{
    /// <summary>読んだ位置を進めた。ほかの参加者の更新日時も進めた</summary>
    Advanced = 0,

    /// <summary>今の読んだ位置が、渡された番号以上だった。何も書かなかった</summary>
    Unchanged = 1,

    /// <summary>自分が参加していない会話、または会話が無い。何も書かなかった</summary>
    NotMember = 2,

    /// <summary>番号が、その会話のメッセージではない。何も書かなかった</summary>
    MessageInvalid = 3,
}
