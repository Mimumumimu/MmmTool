using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Backlog.Main;

/// <summary>Backlog ページ</summary>
public sealed partial class BacklogPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public BacklogViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public BacklogPage(BacklogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
