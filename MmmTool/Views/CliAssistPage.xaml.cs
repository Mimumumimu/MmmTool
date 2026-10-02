using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmTool.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace MmmTool.Views;

public sealed partial class CliAssistPage : Page
{
    public CliAssistViewModel ViewModel { get; }

    public CliAssistPage(CliAssistViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // 入力欄（TextBox）が先にドラッグを処理しても受け取れるよう、処理済みのイベントも拾う
        Composer.AddHandler(DragOverEvent, new DragEventHandler(OnComposerDragOver), handledEventsToo: true);
        Composer.AddHandler(DropEvent, new DragEventHandler(OnComposerDrop), handledEventsToo: true);

        ViewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CliAssistViewModel.CommandItems):
                    RebuildCommandTree();
                    break;
                case nameof(CliAssistViewModel.SelectedCategory):
                    // コマンドの実行などでタブが切り替わったら、タブの見た目も合わせる
                    CategorySelector.SelectedItem = ViewModel.SelectedCategory == CommandCategory.Session
                        ? SessionCategoryItem
                        : TerminalCategoryItem;
                    break;
            }
        };
        RebuildCommandTree();

        ViewModel.FocusRequested += OnFocusRequested;
    }

    private void OnFocusRequested(object? sender, FocusTarget target)
    {
        // クリックしたツリーが自分にフォーカスを取り終えてから移す（すぐ移すとツリーに取り返される）
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (target)
            {
                case FocusTarget.Terminal:
                    TerminalView.FocusTerminal();
                    break;
                case FocusTarget.Input:
                    InputBox.Focus(FocusState.Programmatic);
                    InputBox.SelectionStart = InputBox.Text.Length;
                    break;
            }
        });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await ViewModel.InitializeAsync();

    #region 定型コマンド

    private void OnCategorySelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        => ViewModel.SelectedCategory = sender.SelectedItem == SessionCategoryItem ? CommandCategory.Session : CommandCategory.Terminal;

    /// <summary>ツリーのノードを作り直す。すべてのノードを最初から展開した状態にする。</summary>
    private void RebuildCommandTree()
    {
        CommandTree.RootNodes.Clear();
        foreach (var item in ViewModel.CommandItems)
        {
            CommandTree.RootNodes.Add(CreateNode(item));
        }
    }

    private static TreeViewNode CreateNode(CommandTreeItem item)
    {
        var node = new TreeViewNode { Content = item, IsExpanded = true };
        foreach (var child in item.Children)
        {
            node.Children.Add(CreateNode(child));
        }
        return node;
    }

    // 常に全展開なので、開閉の矢印は見えなくする（行の表示が読み込まれたときに、その行の TreeViewItem へ設定する）
    private void OnCommandTreeItemContentLoaded(object sender, RoutedEventArgs e)
    {
        DependencyObject? current = sender as DependencyObject;
        while (current is not null and not TreeViewItem)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        if (current is TreeViewItem item)
        {
            item.GlyphOpacity = 0;
        }
    }

    private void OnCommandTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        // 手作りのノードでは、InvokedItem はノード自身で、データはその Content にある
        var item = args.InvokedItem is TreeViewNode node ? node.Content : args.InvokedItem;

        // 中間ノードは何もしない（常に展開）
        if (item is CommandTreeItem { Kind: not CommandItemKind.Group } commandItem)
        {
            ViewModel.InvokeCommandItemCommand.Execute(commandItem);
        }
    }

    /// <summary>ツリーが閉じられたら開き直す。</summary>
    /// <remarks>ツリーは常に全展開で表示する（キー操作などで閉じられても開き直す）。</remarks>
    private void OnCommandTreeCollapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        => args.Node.IsExpanded = true;

    #endregion

    #region 送信欄

    private void OnInputPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Ctrl+Enter で送信（Enter だけなら改行）
        if (e.Key == VirtualKey.Enter
            && InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
        {
            e.Handled = true;
            ViewModel.SendCommand.Execute(null);
        }
    }

    private async void OnInputPaste(object sender, TextControlPasteEventArgs e)
    {
        var content = Clipboard.GetContent();

        // エクスプローラーでコピーしたファイルは添付する
        if (content.Contains(StandardDataFormats.StorageItems))
        {
            e.Handled = true;
            var items = await content.GetStorageItemsAsync();
            await ViewModel.AddAttachmentFilesAsync(items.OfType<StorageFile>().Select(file => file.Path));
            return;
        }

        // 画像だけのとき（スクリーンショット等）は添付する。テキストも含むとき（Excel のセル等）は通常の貼り付けにする
        if (content.Contains(StandardDataFormats.Bitmap) && !content.Contains(StandardDataFormats.Text))
        {
            e.Handled = true;
            var bitmap = await content.GetBitmapAsync();
            using var stream = await bitmap.OpenReadAsync();
            await ViewModel.AddAttachmentImageAsync(stream.AsStreamForRead());
        }
    }

    private void OnComposerDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "添付する";
        }
    }

    private async void OnComposerDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        await ViewModel.AddAttachmentFilesAsync(items.OfType<StorageFile>().Select(file => file.Path));
    }

    private void OnRemoveAttachmentClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AttachmentItem item)
        {
            ViewModel.RemoveAttachmentCommand.Execute(item);
        }
    }

    #endregion

    #region 送信履歴

    private void OnHistoryFlyoutOpening(object sender, object e) => ViewModel.PrepareHistory();

    private void OnHistoryItemClick(object sender, ItemClickEventArgs e)
    {
        ViewModel.RestoreHistory((SendHistoryItem)e.ClickedItem);
        HistoryFlyout.Hide();
        InputBox.Focus(FocusState.Programmatic);
        InputBox.SelectionStart = InputBox.Text.Length;
    }

    #endregion
}
