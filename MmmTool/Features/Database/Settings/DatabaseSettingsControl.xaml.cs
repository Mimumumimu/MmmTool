using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Features.Database.Settings;

/// <summary>保存先の設定 (設定ページに並べる部品)</summary>
public sealed partial class DatabaseSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public DatabaseSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public DatabaseSettingsControl(DatabaseSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
