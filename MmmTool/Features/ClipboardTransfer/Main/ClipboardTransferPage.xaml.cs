using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace MmmTool.Features.ClipboardTransfer.Main;

/// <summary>クリップボード転送ページ</summary>
public sealed partial class ClipboardTransferPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public ClipboardTransferViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public ClipboardTransferPage(ClipboardTransferViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>ドロップ領域にファイルが入ったとき、領域を強調する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ドラッグの情報</param>
    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (CanDrop(e))
        {
            VisualStateManager.GoToState(this, "DropActive", true);
        }
    }

    /// <summary>ドロップ領域からファイルが出たとき、強調を戻す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ドラッグの情報</param>
    private void OnDragLeave(object sender, DragEventArgs e) => VisualStateManager.GoToState(this, "DropIdle", true);

    /// <summary>ドラッグ中、ファイルなら送れると示す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ドラッグの情報</param>
    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (CanDrop(e))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "クリップボードにコピー";

            // 中のボタン・文字の上を通ると DragLeave が先に来て強調が消えるので、ここでも強調し直す (同じ状態への切り替えは何も起きない)
            VisualStateManager.GoToState(this, "DropActive", true);
        }
    }

    /// <summary>ドロップされたファイルを、クリップボードに載せる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ドロップの情報</param>
    private async void OnDrop(object sender, DragEventArgs e)
    {
        VisualStateManager.GoToState(this, "DropIdle", true);
        if (!CanDrop(e))
        {
            return;
        }

        try
        {
            var items = await e.DataView.GetStorageItemsAsync();

            // パスの無いファイル (メールの添付など、ディスク上に無いもの)は、中身を読めないので送らない。フォルダーも送らない
            var paths = items.OfType<StorageFile>().Where(file => !string.IsNullOrEmpty(file.Path)).Select(file => file.Path).ToList();
            await ViewModel.SendAsync(paths, items.Count - paths.Count);
        }
        catch (COMException ex)
        {
            ViewModel.Error.Show($"ドロップされたファイルを読めませんでした。{ex.Message}");
        }
    }

    /// <summary>ドロップを受け付けられるか</summary>
    /// <param name="e">ドラッグの情報</param>
    /// <returns>処理中ではなく、ファイルが含まれていれば true</returns>
    private bool CanDrop(DragEventArgs e) => ViewModel.IsIdle && e.DataView.Contains(StandardDataFormats.StorageItems);
}
