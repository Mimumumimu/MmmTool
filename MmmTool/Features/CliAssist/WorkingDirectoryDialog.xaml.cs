using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MmmTool.Features.CliAssist;

/// <summary>作業ディレクトリ変更ダイアログ</summary>
public sealed partial class WorkingDirectoryDialog : ContentDialog
{
    /// <summary>履歴のダブルクリックで確定したか</summary>
    private bool _confirmedByDoubleTap;

    /// <summary>ダイアログの ViewModel</summary>
    public WorkingDirectoryDialogViewModel ViewModel { get; }

    /// <summary>ダイアログを作る</summary>
    /// <param name="viewModel">ダイアログの ViewModel</param>
    public WorkingDirectoryDialog(WorkingDirectoryDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>ダイアログを開き、選ばれたフォルダを返す</summary>
    /// <returns>選ばれたフォルダ。キャンセルなら null</returns>
    /// <remarks>キャンセルなら null。</remarks>
    public async Task<string?> PickAsync()
    {
        ViewModel.Initialize();
        _confirmedByDoubleTap = false;

        var result = await ShowAsync();
        return result == ContentDialogResult.Primary || _confirmedByDoubleTap
            ? ViewModel.DirectoryPath.Trim()
            : null;
    }

    /// <summary>履歴のフォルダがクリックされたら、入力欄に入れる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">クリックされた項目の情報</param>
    private void OnDirectoryClick(object sender, ItemClickEventArgs e)
        => ViewModel.DirectoryPath = (string)e.ClickedItem;

    /// <summary>履歴のフォルダがダブルクリックされたら、入力欄に入れて確定する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ダブルタップの情報</param>
    private void OnDirectoryDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not string directory)
        {
            return;
        }

        ViewModel.DirectoryPath = directory;
        if (ViewModel.IsValid)
        {
            _confirmedByDoubleTap = true;
            Hide();
        }
    }

    /// <summary>履歴の削除ボタンが押されたときの処理</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRemoveDirectoryClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string directory)
        {
            ViewModel.RemoveCommand.Execute(directory);
        }
    }
}
