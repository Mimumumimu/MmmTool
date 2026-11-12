namespace MmmTool.Messages.Core;

/// <summary>会話の種類</summary>
/// <remarks>
/// DB には数値で入る
/// 値を変えると保存済みの内容の意味が変わる
/// </remarks>
public enum ConversationKind
{
    /// <summary>1 対 1</summary>
    /// <remarks>
    /// 同じ 2 人の組に会話は 1 つだけ。
    /// 自分との会話も 1 対 1 で、参加者は自分 1 人。
    /// </remarks>
    Direct = 1,

    /// <summary>グループ</summary>
    /// <remarks>
    /// 名前を持つ
    /// メンバーが同じで名前の違う会話をいくつでも作れる
    /// </remarks>
    Group = 2,
}
