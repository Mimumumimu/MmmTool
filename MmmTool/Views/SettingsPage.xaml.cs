using Microsoft.UI.Xaml.Controls;
using MmmTool.ViewModels;

namespace MmmTool.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
