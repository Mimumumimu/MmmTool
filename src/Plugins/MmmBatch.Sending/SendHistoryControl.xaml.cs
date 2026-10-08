using Microsoft.UI.Xaml.Controls;

namespace MmmBatch.Sending;

/// <summary>送信の状況の一覧の中身 (見出しつきの一覧・エラー)</summary>
public sealed partial class SendHistoryControl : UserControl
{
    /// <summary>一覧の ViewModel</summary>
    public SendHistoryViewModel ViewModel { get; }

    /// <summary>一覧の中身を作る</summary>
    /// <param name="viewModel">一覧の ViewModel</param>
    public SendHistoryControl(SendHistoryViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
