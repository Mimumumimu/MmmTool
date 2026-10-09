using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Features.Users.Settings;

/// <summary>アカウントの設定 (設定ページに並べる部品)</summary>
public sealed partial class AccountSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public AccountSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public AccountSettingsControl(AccountSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // ページの StackPanel の Spacing を取らないよう、使えないときは部品ごと隠す
        Visibility = viewModel.IsAvailable ? Visibility.Visible : Visibility.Collapsed;
    }
}
