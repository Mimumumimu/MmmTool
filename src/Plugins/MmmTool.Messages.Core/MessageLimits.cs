namespace MmmTool.Messages.Core;

/// <summary>メッセージの入力の上限</summary>
/// <remarks>
/// 数え方は UTF-16 の 1 単位 (絵文字は 2)。DB の列の長さ (<c>docs/specs/database.md</c>)と同じ。
/// 長くするときは、<see cref="MessageLimits"/> と DB の列の両方を変える。
/// </remarks>
public static class MessageLimits
{
    /// <summary>タイトルの最大の長さ</summary>
    public const int TitleMaxLength = 100;

    /// <summary>本文の最大の長さ</summary>
    public const int BodyMaxLength = 1000;
}
