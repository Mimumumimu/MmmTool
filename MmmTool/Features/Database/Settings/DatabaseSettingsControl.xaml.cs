using Microsoft.UI.Xaml.Controls;
using MmmTool.Features.Database.Connection;

namespace MmmTool.Features.Database.Settings;

/// <summary>保存先と DB への接続の設定 (設定ページに並べる部品)</summary>
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

        ConnectionHost.Content = new DatabaseConnectionForm(viewModel.Connection);
    }
}
