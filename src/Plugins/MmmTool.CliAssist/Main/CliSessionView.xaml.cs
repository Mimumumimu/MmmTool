using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Clipboards;
using MmmSdk.WinUI.Utilities;
using MmmTool.CliAssist.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace MmmTool.CliAssist.Main;

/// <summary>CLI のセッション 1 つ分の画面 (ターミナルと送信欄)</summary>
public sealed partial class CliSessionView : UserControl
{
    /// <summary>セッションの ViewModel</summary>
    private CliSessionViewModel _viewModel = null!;

    /// <summary>画面を作る</summary>
    /// <remarks>
    /// <see cref="ViewModel"/> と <see cref="WebView2Directory"/> は、画面を表示する前に設定する。
    /// </remarks>
    public CliSessionView()
    {
        InitializeComponent();
        InputBox.KeepCaretVisible();

        // 入力欄 (TextBox)が先にドラッグを処理しても受け取れるよう、処理済みのイベントも拾う
        Composer.AddHandler(DragEnterEvent, new DragEventHandler(OnComposerDragEnter), handledEventsToo: true);
        Composer.AddHandler(DragOverEvent, new DragEventHandler(OnComposerDragOver), handledEventsToo: true);
        Composer.AddHandler(DropEvent, new DragEventHandler(OnComposerDrop), handledEventsToo: true);
    }

    /// <summary>セッションの ViewModel (表示する前に 1 回だけ設定する)</summary>
    public CliSessionViewModel ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            _viewModel.FocusRequested += OnFocusRequested;
            _viewModel.TerminalRestartRequested += OnTerminalRestartRequested;
        }
    }

    /// <summary>ターミナルの起動し直しを求められたら、起動し直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnTerminalRestartRequested(object? sender, EventArgs e) => TerminalView.RestartSessionAsync().Forget();

    /// <summary>WebView2 のデータ (キャッシュなど)の保存先フォルダー (表示する前に設定する)</summary>
    /// <remarks>データのフォルダーの下の <c>WebView2</c>(WebView2 の既定の EXE の横ではなく、データにまとめる)。</remarks>
    public string WebView2Directory { get; set; } = string.Empty;

    /// <summary>ターミナルのコントロールにセッションをつなぐ (つないだとき、シェルが起動する)</summary>
    /// <remarks>起動するシェル (Windows / WSL)は、定型コマンドの読み込みで決まるので、読み込みが済んでから呼ぶ。</remarks>
    public void ConnectTerminal() => TerminalView.Session ??= ViewModel.Terminal;

    /// <summary>ターミナルを閉じ (WebView2 のプロセスも解放する)、ViewModel とのつなぎも外す</summary>
    /// <remarks>
    /// 機能をオフにしたときと、タブを閉じたとき。閉じたあとは、この画面を再利用できない。
    /// シェル (とその中の CLI)は、このあとの ViewModel の後始末 (Dispose)・スコープの破棄で終了する。
    /// つなぎを外すのは、ViewModel が (スコープに)残っても、この画面を残さないため。
    /// </remarks>
    public void Release()
    {
        TerminalView.Close();
        _viewModel.FocusRequested -= OnFocusRequested;
        _viewModel.TerminalRestartRequested -= OnTerminalRestartRequested;
    }

    /// <summary>フォーカスの移動を求められたら、移す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="target">フォーカスを移す先</param>
    private void OnFocusRequested(object? sender, FocusTarget target)
    {
        // クリックしたツリーが自分にフォーカスを取り終えてから移す (すぐ移すとツリーに取り返される)
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

    /// <summary>読み込み時の処理 (入力欄の高さ調整)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnLoaded(object sender, RoutedEventArgs e) => FitInputBoxToThreeLines();

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

    #region 送信欄

    /// <summary>入力欄で Ctrl+Enter が押されたら、送信する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キー入力の情報</param>
    private void OnInputPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Ctrl+Enter で送信 (Enter だけなら改行)
        if (e.Key == VirtualKey.Enter
            && InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
        {
            e.Handled = true;
            ViewModel.SendCommand.Execute(null);
        }
    }

    /// <summary>IME をオンにしたか</summary>
    private bool _imeInitialized;

    /// <summary>入力欄に最初にフォーカスが来たときだけ IME をオンにする (日本語をすぐ打てるように)</summary>
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
    /// <remarks>
    /// このイベントの中では、<c>Clipboard.GetContent()</c> など、コピー元のアプリへ問い合わせるクリップボードの呼び出しをしない。
    /// 返事を待つ間に画面の操作が割り込み、入力欄の貼り付けの途中で WinUI が内部で失敗して落ちるため。
    /// 添付するか (ファイルか、画像だけか)は、問い合わせずに分かる <see cref="ClipboardFormats"/> で決め、
    /// 添付するときだけ、イベントが終わったあとに中身を読む (<see cref="AttachFromClipboardAsync"/>)。
    /// テキストの貼り付けは、入力欄の標準の動きに任せる。
    /// </remarks>
    private void OnInputPaste(object sender, TextControlPasteEventArgs e)
    {
        // エクスプローラーでコピーしたファイル・メールの添付ファイルは添付する。
        // 画像だけのとき (スクリーンショット等)も添付する。テキストも含むとき (Excel のセル等)は通常の貼り付けにする
        if (!ClipboardFormats.HasFiles() && !(ClipboardFormats.HasImage() && !ClipboardFormats.HasText()))
        {
            return;
        }

        e.Handled = true;
        DispatcherQueue.TryEnqueue(AttachFromClipboardAsync);
    }

    /// <summary>クリップボードのファイル・画像を添付する (貼り付けのイベントが終わったあとに呼ぶ)</summary>
    /// <remarks>
    /// クリップボードは同時に 1 つのプロセスしか開けないので、ほかのアプリが開いている瞬間は <see cref="COMException"/> になりうる。このときは添付せず、画面に知らせる。
    /// 形式の判定は目安なので、実際にはファイルも画像も取り出せなかったときは、テキストがあれば、入力欄の選択範囲へ貼り付ける (貼り付けが消えないように)。
    /// </remarks>
    private async void AttachFromClipboardAsync()
    {
        try
        {
            var content = Clipboard.GetContent();

            if (content.Contains(StandardDataFormats.StorageItems))
            {
                await AddAttachmentFilesAsync(await content.GetStorageItemsAsync());
                return;
            }

            if (content.Contains(StandardDataFormats.Bitmap) && !content.Contains(StandardDataFormats.Text))
            {
                var bitmap = await content.GetBitmapAsync();
                using var stream = await bitmap.OpenReadAsync();
                await ViewModel.AddAttachmentImageAsync(stream.AsStreamForRead());
                return;
            }

            if (content.Contains(StandardDataFormats.Text))
            {
                InputBox.SelectedText = await content.GetTextAsync();
            }
        }
        catch (COMException ex)
        {
            ViewModel.Error.Show($"クリップボードを読めませんでした。もう一度貼り付けてください。{ex.Message}");
        }
    }

    /// <summary>ドラッグ中に出す案内の文言</summary>
    private string _dropCaption = AttachCaption;

    /// <summary>ファイルを含むドラッグの案内</summary>
    private const string AttachCaption = "添付する";

    /// <summary>フォルダーだけのドラッグの案内</summary>
    private const string PastePathCaption = "パスを貼り付ける";

    /// <summary>ドラッグが入ったとき、中身がフォルダーだけかを調べて、案内の文言を決める</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ドラッグの情報</param>
    /// <remarks>
    /// 中身の読み取りは非同期なので、ドラッグごとに 1 回だけここで行う。ドラッグ中に何度も呼ばれる <see cref="OnComposerDragOver"/> では行わない。
    /// 読めなかったときは、既定の「添付する」のままにして、読めなかったことはドロップのときに知らせる。
    /// </remarks>
    private async void OnComposerDragEnter(object sender, DragEventArgs e)
    {
        _dropCaption = AttachCaption;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items.All(item => item.IsOfType(StorageItemTypes.Folder)))
            {
                _dropCaption = PastePathCaption;
            }
        }
        catch (COMException)
        {
            // 案内の文言を決められないだけなので、既定のまま続ける
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>ドラッグ中、ファイルなら添付、フォルダーならパスを貼り付けると示す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ドラッグの情報</param>
    private void OnComposerDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = _dropCaption;
        }
    }

    /// <summary>ドロップされたファイルを添付し、フォルダーのパスを入力欄に貼り付ける</summary>
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
            var items = await e.DataView.GetStorageItemsAsync();
            await AddAttachmentFilesAsync(items);
            PasteFolderPaths(items.OfType<StorageFolder>());
        }
        catch (COMException ex)
        {
            ViewModel.Error.Show($"ドロップされたファイルを読めませんでした。{ex.Message}");
        }
    }

    /// <summary>ドロップされたフォルダーのパスを、入力欄の選択範囲へ入れる</summary>
    /// <param name="folders">ドロップされたフォルダー</param>
    /// <remarks>選択範囲があれば置き換える。入れたあとは、入力欄にフォーカスを移し、入れた文字の後ろにカーソルを置く。</remarks>
    private void PasteFolderPaths(IEnumerable<StorageFolder> folders)
    {
        // パスの無いフォルダー (仮想フォルダーなど)は、入れられない
        var text = ViewModel.CreateFolderPathText(folders.Select(folder => folder.Path).Where(path => !string.IsNullOrEmpty(path)));
        if (text is null)
        {
            return;
        }

        var start = InputBox.SelectionStart;
        InputBox.SelectedText = text;
        InputBox.Focus(FocusState.Programmatic);
        InputBox.SelectionStart = start + text.Length;
        InputBox.SelectionLength = 0;
    }

    /// <summary>ドロップ・貼り付けされたファイルを添付する</summary>
    /// <param name="items">ドロップ・貼り付けされた項目 (フォルダーは添付しない)</param>
    /// <returns>添付の完了を表すタスク</returns>
    /// <remarks>
    /// ディスク上のファイルは、元のパスをそのまま添付する。
    /// パスの無いファイル (メールの添付ファイルなど、ディスク上に無いもの)は、中身を読んで一時保存する。
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

    #region よく使う文

    /// <summary>よく使う文のチップが押されたら、入力欄に入れる (送信はしない)</summary>
    /// <param name="sender">イベントの送信元 (チップのボタン)</param>
    /// <param name="e">イベントの情報</param>
    private void OnQuickMessageClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is QuickMessageItem item)
        {
            ViewModel.UseQuickMessage(item);
            InputBox.Focus(FocusState.Programmatic);
            InputBox.SelectionStart = InputBox.Text.Length;
        }
    }

    #endregion
}
