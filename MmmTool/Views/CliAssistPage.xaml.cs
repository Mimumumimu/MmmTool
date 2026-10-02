using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmTool.Interop;
using MmmTool.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace MmmTool.Views;

/// <summary>CLI補助ページ</summary>
public sealed partial class CliAssistPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public CliAssistViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
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

    /// <summary>フォーカスの移動を求められたら、移す</summary>
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

    /// <summary>読み込み時の処理（入力欄の高さ調整と ViewModel の初期化）</summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitInputBoxToThreeLines();
        await ViewModel.InitializeAsync();
    }

    /// <summary>入力欄の高さを、実際の 1 行の高さ × 3 行に合わせる</summary>
    /// <remarks>固定値だと、フォントによって下だけ余る・欠けるため。</remarks>
    private void FitInputBoxToThreeLines()
    {
        const int lines = 3;
        var probe = new TextBlock
        {
            Text = "あ",
            FontFamily = InputBox.FontFamily,
            FontSize = InputBox.FontSize,
        };
        probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        InputBox.Height = probe.DesiredSize.Height * lines
            + InputBox.Padding.Top + InputBox.Padding.Bottom
            + InputBox.BorderThickness.Top + InputBox.BorderThickness.Bottom;
    }

    #region 定型コマンド

    /// <summary>タブが選ばれたら、ViewModel に反映する</summary>
    private void OnCategorySelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        => ViewModel.SelectedCategory = sender.SelectedItem == SessionCategoryItem ? CommandCategory.Session : CommandCategory.Terminal;

    /// <summary>ツリーのノードを作り直す</summary>
    /// <remarks>すべてのノードを最初から展開した状態にする。</remarks>
    private void RebuildCommandTree()
    {
        CommandTree.RootNodes.Clear();
        foreach (var item in ViewModel.CommandItems)
        {
            CommandTree.RootNodes.Add(CreateNode(item));
        }
    }

    /// <summary>ViewModel の要素から、ツリーのノードを作る（子も展開した状態）</summary>
    private static TreeViewNode CreateNode(CommandTreeItem item)
    {
        var node = new TreeViewNode { Content = item, IsExpanded = true };
        foreach (var child in item.Children)
        {
            node.Children.Add(CreateNode(child));
        }
        return node;
    }

    /// <summary>ツリーを横にもスクロールできるようにする。</summary>
    /// <remarks>TreeView は既定で横スクロールが無効（長い名前は右が切れる）。テンプレート内の ScrollViewer に直接設定する。</remarks>
    private void OnCommandTreeLoaded(object sender, RoutedEventArgs e)
    {
        if (FindDescendant<ScrollViewer>(CommandTree) is { } scrollViewer)
        {
            scrollViewer.HorizontalScrollMode = ScrollMode.Auto;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    /// <summary>子孫から、指定の型の要素を探す</summary>
    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if ((child as T ?? FindDescendant<T>(child)) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>ツリーの項目が選ばれたら、コマンドを実行する</summary>
    private void OnCommandTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        // 手作りのノードでは、InvokedItem はノード自身で、データはその Content にある
        var item = args.InvokedItem is TreeViewNode node ? node.Content : args.InvokedItem;

        // 中間ノードは開閉だけ（TreeView が行う）
        if (item is CommandTreeItem { Kind: not CommandItemKind.Group } commandItem)
        {
            ViewModel.InvokeCommandItemCommand.Execute(commandItem);
        }
    }

    #endregion

    #region 送信欄

    /// <summary>入力欄で Ctrl+Enter が押されたら、送信する</summary>
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

    /// <summary>IME をオンにしたか</summary>
    private bool _imeInitialized;

    /// <summary>入力欄に最初にフォーカスが来たときだけ IME をオンにする（日本語をすぐ打てるように）</summary>
    /// <remarks>以降はユーザーの切り替えを尊重する。</remarks>
    private void OnInputGotFocus(object sender, RoutedEventArgs e)
    {
        if (_imeInitialized) return;
        _imeInitialized = true;
        NativeMethods.TurnOnImeForFocusedWindow();
    }

    /// <summary>貼り付けられたものがファイルや画像なら、添付する</summary>
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

    /// <summary>ドラッグ中、ファイルなら添付できると示す</summary>
    private void OnComposerDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "添付する";
        }
    }

    /// <summary>ドロップされたファイルを添付する</summary>
    private async void OnComposerDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        await ViewModel.AddAttachmentFilesAsync(items.OfType<StorageFile>().Select(file => file.Path));
    }

    /// <summary>添付の削除ボタンが押されたときの処理</summary>
    private void OnRemoveAttachmentClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AttachmentItem item)
        {
            ViewModel.RemoveAttachmentCommand.Execute(item);
        }
    }

    #endregion

    #region 送信履歴

    /// <summary>履歴の一覧が開くときの処理</summary>
    private void OnHistoryFlyoutOpening(object sender, object e) => ViewModel.PrepareHistory();

    /// <summary>履歴の項目が選ばれたら、入力欄に戻す</summary>
    private void OnHistoryItemClick(object sender, ItemClickEventArgs e)
    {
        ViewModel.RestoreHistory((SendHistoryItem)e.ClickedItem);
        HistoryFlyout.Hide();
        InputBox.Focus(FocusState.Programmatic);
        InputBox.SelectionStart = InputBox.Text.Length;
    }

    #endregion
}
