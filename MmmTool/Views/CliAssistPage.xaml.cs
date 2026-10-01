using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmTool.ViewModels;

namespace MmmTool.Views;

public sealed partial class CliAssistPage : Page
{
    public CliAssistViewModel ViewModel { get; }

    public CliAssistPage(CliAssistViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    // ②で実装: 選んだ定型コマンドを ViewModel へ渡す
    private void OnCommandTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
    }

    // ②で実装: クリップボードの画像・ファイルを添付として受け取る（テキストは既定の貼り付けのまま）
    private void OnInputPaste(object sender, TextControlPasteEventArgs e)
    {
    }

    // ②で実装: ドラッグ中のファイルを受け入れ可能か判定する
    private void OnInputDragOver(object sender, DragEventArgs e)
    {
    }

    // ②で実装: ドロップされたファイルを添付として受け取る
    private void OnInputDrop(object sender, DragEventArgs e)
    {
    }
}
