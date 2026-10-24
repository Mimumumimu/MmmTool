using Microsoft.UI.Xaml.Controls;

namespace MmmTool.WorkItems.Settings;

/// <summary>作業管理の設定 (設定ページに並べる部品)</summary>
public sealed partial class WorkItemSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public WorkItemSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public WorkItemSettingsControl(WorkItemSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
