using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Messages.Main;

/// <summary>メッセージ送信のページ (サイドバー)</summary>
public sealed partial class MessageSendPage : Page
{
    /// <summary>ページを作る</summary>
    /// <param name="viewModel">入力欄の ViewModel</param>
    public MessageSendPage(MessageSendViewModel viewModel)
    {
        InitializeComponent();
        SendControl.ViewModel = viewModel;
    }
}
