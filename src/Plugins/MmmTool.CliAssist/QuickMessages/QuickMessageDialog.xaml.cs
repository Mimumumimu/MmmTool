using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ダイアログ</summary>
public sealed partial class QuickMessageDialog : ContentDialog, ICliEditDialog
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
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.EditRequested += (_, _) => FocusEditBox();

        // ドロップの処理の途中では一覧を変えられないので、処理が終わってから、グループの入れ子を直す
        ViewModel.GroupNested += (_, _) => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ViewModel.MoveNestedGroupsOut);
    }

    /// <inheritdoc />
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

    /// <summary>ツリーの選択が変わったら、ViewModel に反映する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">選択の変更の情報</param>
    /// <remarks>TreeView.SelectedItem は x:Bind の双方向にできないため、選択はここで ViewModel と相互に合わせる。</remarks>
    private void OnTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        => ViewModel.SelectedItem = args.GetAddedItem<QuickMessageEditItem>();

    /// <summary>ドラッグを始めたとき、グループをドラッグしているなら、グループの行へ落とせなくする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">ドラッグの情報</param>
    /// <remarks>
    /// グループの中にグループは入れないため。文をドラッグしているときは、グループの行へ落とせる状態に戻す。
    /// ドラッグの終わりの通知は当てにせず、始めるたびに毎回決め直す。
    /// グループの中の文の間へ落ちたものは、ここでは防げないので、ViewModel が外へ出す。
    /// </remarks>
    private void OnTreeDragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs args)
    {
        var draggingGroup = args.Items.OfType<QuickMessageEditItem>().Any(item => item.IsGroup);
        foreach (var group in ViewModel.RootItems.Where(item => item.IsGroup))
        {
            if (Tree.ContainerFromItem(group) is TreeViewItem container)
            {
                container.AllowDrop = !draggingGroup;
            }
        }
    }

    /// <summary>ViewModel の選択が変わったら、ツリーの選択を合わせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuickMessageDialogViewModel.SelectedItem) && Tree.SelectedItem != ViewModel.SelectedItem)
        {
            // 追加直後は行がまだできていないので、表示が追いついてから選ぶ
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => Tree.SelectedItem = ViewModel.SelectedItem);
        }
    }

    /// <summary>選んだ項目の入力欄にフォーカスして、すぐ打てるようにする</summary>
    /// <remarks>グループはグループ名、文は文の欄。選択の切り替えで入力欄が表示されてから移す。</remarks>
    private void FocusEditBox()
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (ViewModel.SelectedItem is { IsGroup: true })
            {
                GroupNameBox.Focus(FocusState.Programmatic);
                GroupNameBox.SelectAll();
            }
            else if (ViewModel.SelectedItem is not null)
            {
                MessageTextBox.Focus(FocusState.Programmatic);
            }
        });
    }
}
