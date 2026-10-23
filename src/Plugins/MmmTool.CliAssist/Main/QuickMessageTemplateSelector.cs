using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.CliAssist.Main;

/// <summary>よく使う文のチップの見た目を、文とグループで切り替える</summary>
public sealed partial class QuickMessageTemplateSelector : DataTemplateSelector
{
    /// <summary>文のチップの見た目</summary>
    public DataTemplate? MessageTemplate { get; set; }

    /// <summary>グループのチップの見た目</summary>
    public DataTemplate? GroupTemplate { get; set; }

    /// <inheritdoc />
    protected override DataTemplate? SelectTemplateCore(object item) => item is QuickMessageItem { IsGroup: true } ? GroupTemplate : MessageTemplate;

    /// <inheritdoc />
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
