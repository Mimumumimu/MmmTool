using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Utilities;

namespace MmmTool.Reminders.Settings;

/// <summary>設定ページの「通知を受け取る機能」。設定ページに並べる部品で、サーバーモードだけ出る</summary>
public sealed partial class NotificationFeatureSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public NotificationFeatureSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public NotificationFeatureSettingsControl(NotificationFeatureSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Visibility = viewModel.IsAvailable ? Visibility.Visible : Visibility.Collapsed;

        // 設定ページを開くたびに、読み直す
        Loaded += (_, _) => ViewModel.InitializeAsync().Forget();
    }
}
