using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>
/// 宛先に探した人 1 人。画面に出す文字。
/// </summary>
/// <param name="source">探した人</param>
public sealed class PersonItem(MessagePerson source)
{
    /// <summary>元の人</summary>
    public MessagePerson Source { get; } = source;

    /// <summary>表示名</summary>
    public string DisplayName { get; } = source.DisplayName;
}
