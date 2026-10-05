using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Utilities;
using MmmTool.Core.CliAssist;
using MmmTool.Shell;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace MmmTool.Features.CliAssist.Main;

/// <summary>CLI補助ページ</summary>
public sealed partial class CliAssistPage : Page, IReleasablePage
{
    /// <summary>ページの ViewModel</summary>
    public CliAssistViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
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
                        : ShellCategoryItem;
                    break;
            }
        };
        RebuildCommandTree();

        ViewModel.FocusRequested += OnFocusRequested;
        ViewModel.TerminalRestartRequested += (_, _) => TerminalView.RestartSessionAsync().Forget();
    }

    /// <inheritdoc />
    /// <remarks>機能をオフにしたとき。ターミナルのコントロールからセッションを外す。シェル（とその中の CLI）は、このあとのスコープの破棄で終了する。</remarks>
    public void Release() => TerminalView.Session = null;

    /// <summary>フォーカスの移動を求められたら、移す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="target">フォーカスを移す先</param>
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

    /// <summary>読み込み時の処理（入力欄の高さ調整・ViewModel の初期化・ターミナルの接続）</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 起動するシェル（Windows / WSL）は、初期化で読み込む定型コマンドで決まるので、初期化が済んでからターミナルにつなぐ。
    /// ターミナルは、つないだとき（表示の準備がまだなら、準備ができたとき）にシェルを起動する。
    /// </remarks>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitInputBoxToThreeLines();
        await ViewModel.InitializeAsync();
        TerminalView.Session ??= ViewModel.Terminal;
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
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">選択の変更の情報</param>
    private void OnCategorySelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        => ViewModel.SelectedCategory = sender.SelectedItem == SessionCategoryItem ? CommandCategory.Session : CommandCategory.Shell;

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
    /// <param name="item">ツリーの要素</param>
    /// <returns>ツリーのノード</returns>
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
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>TreeView は既定で横スクロールが無効（長い名前は右が切れる）。テンプレート内の ScrollViewer に直接設定する。</remarks>
    private void OnCommandTreeLoaded(object sender, RoutedEventArgs e)
    {
        if (VisualTreeSearch.FindDescendant<ScrollViewer>(CommandTree) is { } scrollViewer)
        {
            scrollViewer.HorizontalScrollMode = ScrollMode.Auto;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    /// <summary>ツリーの項目が選ばれたら、コマンドを実行する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">選ばれた項目の情報</param>
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
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キー入力の情報</param>
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
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>以降はユーザーの切り替えを尊重する。</remarks>
    private void OnInputGotFocus(object sender, RoutedEventArgs e)
    {
        if (_imeInitialized) return;
        _imeInitialized = true;
        ImeControl.TurnOn();
    }

    /// <summary>貼り付けられたものがファイルや画像なら、添付する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">貼り付けの情報</param>
    /// <remarks>クリップボードは同時に 1 つのプロセスしか開けないので、ほかのアプリが開いている瞬間は <see cref="COMException"/> になりうる。このときは添付せず、画面に知らせる。</remarks>
    private async void OnInputPaste(object sender, TextControlPasteEventArgs e)
    {
        try
        {
            var content = Clipboard.GetContent();

            // エクスプローラーでコピーしたファイルは添付する
            if (content.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = true;
                await AddAttachmentFilesAsync(await content.GetStorageItemsAsync());
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
        catch (COMException ex)
        {
            ViewModel.Error.Show($"クリップボードを読めませんでした。もう一度貼り付けてください。{ex.Message}");
        }
    }

    /// <summary>ドラッグ中、ファイルなら添付できると示す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ドラッグの情報</param>
    private void OnComposerDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "添付する";
        }
    }

    /// <summary>ドロップされたファイルを添付する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ドロップの情報</param>
    private async void OnComposerDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        try
        {
            await AddAttachmentFilesAsync(await e.DataView.GetStorageItemsAsync());
        }
        catch (COMException ex)
        {
            ViewModel.Error.Show($"ドロップされたファイルを読めませんでした。{ex.Message}");
        }
    }

    /// <summary>ドロップ・貼り付けされたファイルを添付する</summary>
    /// <param name="items">ドロップ・貼り付けされた項目（フォルダーは添付しない）</param>
    /// <returns>添付の完了を表すタスク</returns>
    /// <remarks>
    /// ディスク上のファイルは、元のパスをそのまま添付する。
    /// パスの無いファイル（メールの添付ファイルなど、ディスク上に無いもの）は、中身を読んで一時保存する。
    /// </remarks>
    private async Task AddAttachmentFilesAsync(IReadOnlyList<IStorageItem> items)
    {
        foreach (var file in items.OfType<StorageFile>())
        {
            if (!string.IsNullOrEmpty(file.Path))
            {
                ViewModel.AddAttachmentFile(file.Path);
                continue;
            }

            try
            {
                using var content = await file.OpenStreamForReadAsync();
                await ViewModel.AddAttachmentContentAsync(content, file.Name);
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
            {
                ViewModel.Error.Show($"{file.Name} を添付できませんでした。{ex.Message}");
            }
        }
    }

    /// <summary>添付の削除ボタンが押されたときの処理</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
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
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnHistoryFlyoutOpening(object sender, object e) => ViewModel.PrepareHistory();

    /// <summary>履歴の項目が選ばれたら、入力欄に戻す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">クリックされた項目の情報</param>
    private void OnHistoryItemClick(object sender, ItemClickEventArgs e)
    {
        ViewModel.RestoreHistory((SendHistoryItem)e.ClickedItem);
        HistoryFlyout.Hide();
        InputBox.Focus(FocusState.Programmatic);
        InputBox.SelectionStart = InputBox.Text.Length;
    }

    #endregion
}
