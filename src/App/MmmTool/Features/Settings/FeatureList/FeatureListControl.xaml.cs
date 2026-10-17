using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Features.Settings.FeatureList;

/// <summary>設定ページの「機能」の一覧 (設定ページに並べる部品)</summary>
public sealed partial class FeatureListControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public FeatureListViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public FeatureListControl(FeatureListViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>「既定の順に戻す」を押したときの処理</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnResetOrderClick(object sender, RoutedEventArgs e)
        => await ViewModel.ResetOrderAsync();

    /// <summary>機能のスイッチが切り替わったときの処理</summary>
    /// <param name="sender">イベントの送信元 (スイッチ。データの行を持つ)</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>利用者が操作したスイッチだけを扱う。並べ替えや「既定の順に戻す」で行の部品が使い回されると、別の行の状態のままスイッチの切り替えが通知されるため、フォーカスの無いスイッチは無視する。</remarks>
    private async void OnFeatureToggled(object sender, RoutedEventArgs e)
    {
        // バインドの書き戻しより先に呼ばれることがあるので、スイッチの状態を行へ写してから渡す
        if (sender is ToggleSwitch { DataContext: FeatureItem item, FocusState: not FocusState.Unfocused } toggle)
        {
            item.IsOn = toggle.IsOn;
            await ViewModel.ToggleFeatureAsync(item);
        }
    }
}
