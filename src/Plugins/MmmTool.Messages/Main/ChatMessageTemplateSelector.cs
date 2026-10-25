using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Messages.Main;

/// <summary>
/// メッセージの見た目を、自分が送ったものと、受け取ったもので切り替える。
/// </summary>
/// <remarks>自分が送ったメッセージは右に寄せて強調色、受け取ったメッセージは左に寄せてカードの色にする。</remarks>
public sealed partial class ChatMessageTemplateSelector : DataTemplateSelector
{
    /// <summary>自分が送ったメッセージの見た目</summary>
    public DataTemplate? MineTemplate { get; set; }

    /// <summary>受け取ったメッセージの見た目</summary>
    public DataTemplate? OtherTemplate { get; set; }

    /// <inheritdoc />
    protected override DataTemplate? SelectTemplateCore(object item) => item is ChatMessageItem { IsMine: true } ? MineTemplate : OtherTemplate;

    /// <inheritdoc />
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
