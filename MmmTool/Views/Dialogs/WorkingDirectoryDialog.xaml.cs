using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmTool.ViewModels;

namespace MmmTool.Views.Dialogs;

public sealed partial class WorkingDirectoryDialog : ContentDialog
{
    private bool _confirmedByDoubleTap;

    public WorkingDirectoryDialogViewModel ViewModel { get; }

    public WorkingDirectoryDialog(WorkingDirectoryDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>ダイアログを開き、選ばれたフォルダを返す。キャンセルなら null。</summary>
    public async Task<string?> PickAsync()
    {
        ViewModel.Initialize();
        _confirmedByDoubleTap = false;

        var result = await ShowAsync();
        return result == ContentDialogResult.Primary || _confirmedByDoubleTap
            ? ViewModel.DirectoryPath.Trim()
            : null;
    }

    private void OnDirectoryClick(object sender, ItemClickEventArgs e)
        => ViewModel.DirectoryPath = (string)e.ClickedItem;

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

    private void OnRemoveDirectoryClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string directory)
        {
            ViewModel.RemoveCommand.Execute(directory);
        }
    }
}
