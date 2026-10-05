using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Features.Backlog.Settings;

/// <summary>Backlog 連携の設定 (設定ページに並べる部品)</summary>
public sealed partial class BacklogSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public BacklogSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public BacklogSettingsControl(BacklogSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>入力欄からフォーカスが外れたとき、API キーを保存する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>1 文字ごとに保存すると、入力途中のキーを保管庫に書いてしまうので、入力を終えたときに保存する。</remarks>
    private void OnApiKeyLostFocus(object sender, RoutedEventArgs e) => ViewModel.Save(ApiKeyBox.Password);
}
