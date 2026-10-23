using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.CliAssist.FocusTriggers;

/// <summary>ターミナルへ移る文字列の編集ダイアログ</summary>
public sealed partial class FocusTriggerDialog : ContentDialog, ICliEditDialog
{
    /// <summary>ダイアログの ViewModel</summary>
    public FocusTriggerDialogViewModel ViewModel { get; }

    /// <summary>ダイアログを作る</summary>
    /// <param name="viewModel">ダイアログの ViewModel</param>
    public FocusTriggerDialog(FocusTriggerDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        ViewModel.FocusRequested += OnFocusRequested;
    }

    /// <inheritdoc />
    public async Task EditAsync()
    {
        ViewModel.Initialize();
        await ShowAsync();
    }

    /// <summary>項目の入力欄へ、フォーカスを移す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="item">フォーカスを移す項目</param>
    /// <remarks>項目を足した直後は、入力欄がまだ作られていないので、画面の配置を済ませてから探す。</remarks>
    private void OnFocusRequested(object? sender, FocusTriggerEditItem item)
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            TriggerList.UpdateLayout();
            if (TriggerList.ContainerFromItem(item) is DependencyObject container
                && VisualTreeSearch.FindDescendant<TextBox>(container) is { } box)
            {
                box.StartBringIntoView();
                box.Focus(FocusState.Programmatic);
            }
        });
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
