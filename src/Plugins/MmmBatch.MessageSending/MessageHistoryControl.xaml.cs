using Microsoft.UI.Xaml.Controls;

namespace MmmBatch.MessageSending;

/// <summary>メッセージの送信の履歴の一覧の中身 (見出しつきの一覧・エラー)</summary>
public sealed partial class MessageHistoryControl : UserControl
{
    /// <summary>一覧の ViewModel</summary>
    public MessageHistoryViewModel ViewModel { get; }

    /// <summary>一覧の中身を作る</summary>
    /// <param name="viewModel">一覧の ViewModel</param>
    public MessageHistoryControl(MessageHistoryViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
