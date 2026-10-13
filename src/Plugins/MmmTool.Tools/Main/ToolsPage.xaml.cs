using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Tools.Main;

/// <summary>ツールページ</summary>
public sealed partial class ToolsPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public ToolsViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public ToolsPage(ToolsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
