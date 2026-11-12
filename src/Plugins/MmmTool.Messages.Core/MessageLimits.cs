namespace MmmTool.Messages.Core;

/// <summary>メッセージの入力の上限</summary>
/// <remarks>
/// 数え方は UTF-16 の 1 単位で、絵文字は 2。
/// DB の列の長さ (<c>docs/specs/database.md</c>)と同じ。
/// 長くするときは <see cref="MessageLimits"/> と DB の列の両方を変える。
/// </remarks>
public static class MessageLimits
{
    /// <summary>本文の最大の長さ</summary>
    public const int BodyMaxLength = 1000;

    /// <summary>グループの名前の最大の長さ</summary>
    /// <remarks>MessageConversation.Name の列の長さと同じ</remarks>
    public const int GroupNameMaxLength = 50;

    /// <summary>グループの参加者の最大の人数</summary>
    /// <remarks>自分を含む</remarks>
    public const int GroupMaxMembers = 50;
}
