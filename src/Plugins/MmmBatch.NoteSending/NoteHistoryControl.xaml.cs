using Microsoft.UI.Xaml.Controls;

namespace MmmBatch.NoteSending;

/// <summary>ノートの送信の履歴の一覧の中身 (見出しつきの一覧・エラー)</summary>
public sealed partial class NoteHistoryControl : UserControl
{
    /// <summary>一覧の ViewModel</summary>
    public NoteHistoryViewModel ViewModel { get; }

    /// <summary>一覧の中身を作る</summary>
    /// <param name="viewModel">一覧の ViewModel</param>
    public NoteHistoryControl(NoteHistoryViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
