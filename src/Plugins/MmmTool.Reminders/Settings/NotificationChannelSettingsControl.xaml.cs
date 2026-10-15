using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Reminders.Settings;

/// <summary>設定ページの「送信先」 (設定ページに並べる部品。サーバーモードだけ)</summary>
public sealed partial class NotificationChannelSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public NotificationChannelSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public NotificationChannelSettingsControl(NotificationChannelSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Visibility = viewModel.IsAvailable ? Visibility.Visible : Visibility.Collapsed;
    }
}
