using Microsoft.UI.Xaml.Controls;
using MmmTool.ViewModels;

namespace MmmTool.Views;

/// <summary>設定ページ</summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
