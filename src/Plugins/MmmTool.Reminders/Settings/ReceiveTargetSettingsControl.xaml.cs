using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Utilities;

namespace MmmTool.Reminders.Settings;

/// <summary>設定ページの「受け取り先」。設定ページに並べる部品で、サーバーモードだけ出る</summary>
public sealed partial class ReceiveTargetSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public ReceiveTargetSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public ReceiveTargetSettingsControl(ReceiveTargetSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Visibility = viewModel.IsAvailable ? Visibility.Visible : Visibility.Collapsed;

        // 設定ページを開くたびに、送信先の登録の変わりを取り込む
        Loaded += (_, _) => ViewModel.InitializeAsync().Forget();
    }
}
