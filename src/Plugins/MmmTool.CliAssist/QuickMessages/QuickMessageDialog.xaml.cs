using Microsoft.UI.Xaml.Controls;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ダイアログ</summary>
public sealed partial class QuickMessageDialog : ContentDialog
{
    /// <summary>ダイアログの ViewModel</summary>
    public QuickMessageDialogViewModel ViewModel { get; }

    /// <summary>ダイアログを作る</summary>
    /// <param name="viewModel">ダイアログの ViewModel</param>
    public QuickMessageDialog(QuickMessageDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    /// <summary>ダイアログを開く</summary>
    /// <returns>ダイアログが閉じるまでの完了を表すタスク</returns>
    public async Task EditAsync()
    {
        ViewModel.Initialize();
        await ShowAsync();
    }

    /// <summary>「保存」が押されたとき、保存できるまでダイアログを閉じない</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            e.Cancel = !await ViewModel.SaveAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }
}
