using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist.WorkingDirectory;

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
    /// <param name="title">ダイアログのタイトル</param>
    /// <param name="primaryButtonText">決定ボタンの文言</param>
    /// <param name="openDirectories">ほかのタブが開いているフォルダ (選べないようにする)</param>
    /// <returns>選ばれたフォルダ。キャンセルなら null</returns>
    public async Task<string?> PickAsync(string title, string primaryButtonText, IReadOnlyList<string> openDirectories)
    {
        Title = title;
        PrimaryButtonText = primaryButtonText;
        ViewModel.Initialize(openDirectories);
        _confirmedByDoubleTap = false;

        var result = await ShowAsync();
        return result == ContentDialogResult.Primary || _confirmedByDoubleTap
            ? SessionDirectory.ToWorkingDirectory(ViewModel.DirectoryPath)
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
    private async void OnDirectoryDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not string directory)
        {
            return;
        }

        if (await ViewModel.UseDirectoryAsync(directory))
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
