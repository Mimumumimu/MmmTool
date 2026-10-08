using Microsoft.UI.Xaml.Controls;

namespace MmmBatch.Sending;

/// <summary>送信の状況の画面 (「今日」の送信予定と、「履歴」を切り替える)</summary>
/// <remarks>最初は「今日」を出す。2 つの画面は、作ったあと使い回す (切り替えのたびに作り直さない)。</remarks>
public sealed partial class SendMainControl : UserControl
{
    /// <summary>今日の送信予定の画面</summary>
    private readonly SendPlanControl _plan;

    /// <summary>送信の履歴の画面</summary>
    private readonly SendHistoryControl _history;

    /// <summary>画面を作る</summary>
    /// <param name="plan">今日の送信予定の画面</param>
    /// <param name="history">送信の履歴の画面</param>
    public SendMainControl(SendPlanControl plan, SendHistoryControl history)
    {
        _plan = plan;
        _history = history;
        InitializeComponent();
        ContentHost.Content = plan;
    }

    /// <summary>切り替えで、出す画面を替える</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">イベントの情報</param>
    private void OnSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        => ContentHost.Content = sender.SelectedItem == HistoryItem ? _history : _plan;
}
