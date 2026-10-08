using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Features.Settings.General;

/// <summary>メインウィンドウの設定 (設定ページに並べる部品)</summary>
public sealed partial class MainWindowSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public MainWindowSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public MainWindowSettingsControl(MainWindowSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
