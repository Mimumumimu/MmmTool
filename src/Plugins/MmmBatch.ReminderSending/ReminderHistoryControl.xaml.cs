using Microsoft.UI.Xaml.Controls;

namespace MmmBatch.ReminderSending;

/// <summary>リマインダーの送信の履歴の一覧の中身 (見出しつきの一覧・エラー)</summary>
public sealed partial class ReminderHistoryControl : UserControl
{
    /// <summary>一覧の ViewModel</summary>
    public ReminderHistoryViewModel ViewModel { get; }

    /// <summary>一覧の中身を作る</summary>
    /// <param name="viewModel">一覧の ViewModel</param>
    public ReminderHistoryControl(ReminderHistoryViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
