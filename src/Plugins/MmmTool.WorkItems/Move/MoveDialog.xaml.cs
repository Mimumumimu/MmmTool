using Microsoft.UI.Xaml.Controls;

namespace MmmTool.WorkItems.Move;

/// <summary>移動先のグループか案件を選ぶダイアログ</summary>
public sealed partial class MoveDialog : ContentDialog
{
    /// <summary>ダイアログを作る</summary>
    public MoveDialog() => InitializeComponent();

    /// <summary>ダイアログを開き、選ばれた移動先を返す</summary>
    /// <param name="destinations">選べる移動先</param>
    /// <returns>選ばれた移動先。キャンセルなら null</returns>
    public async Task<MoveDestination?> PickAsync(IReadOnlyList<MoveDestination> destinations)
    {
        DestinationList.ItemsSource = destinations;
        var result = await ShowAsync();
        return result == ContentDialogResult.Primary ? DestinationList.SelectedItem as MoveDestination : null;
    }

    /// <summary>選ぶと、「移動」を押せるようにする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">選択の変更の情報</param>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => IsPrimaryButtonEnabled = DestinationList.SelectedItem is not null;
}
