using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Utilities;

namespace MmmBatch.ReminderSending;

/// <summary>「リマインダーの送信」のページ (「今日」の送信予定と、「履歴」を切り替える)</summary>
/// <remarks>
/// 最初は「今日」を出す。2 つの画面は、作ったあと使い回す (切り替えのたびに作り直さない)。
/// ページを出すたびに、一覧を読み直す (出していない間に変わった分を、すぐ見せるため)。
/// </remarks>
public sealed partial class ReminderSendingPage : Page
{
    /// <summary>今日の送信予定の画面</summary>
    private readonly ReminderPlanControl _plan;

    /// <summary>送信の履歴の画面</summary>
    private readonly ReminderHistoryControl _history;

    /// <summary>ページを作る</summary>
    /// <param name="plan">今日の送信予定の画面</param>
    /// <param name="history">送信の履歴の画面</param>
    public ReminderSendingPage(ReminderPlanControl plan, ReminderHistoryControl history)
    {
        _plan = plan;
        _history = history;
        InitializeComponent();
        ContentHost.Content = plan;
        Loaded += OnLoaded;
    }

    /// <summary>ページを出したら、一覧を読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _plan.ViewModel.RefreshAsync().Forget();
        _history.ViewModel.RefreshAsync().Forget();
    }

    /// <summary>切り替えで、出す画面を替える</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">イベントの情報</param>
    private void OnSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        => ContentHost.Content = sender.SelectedItem == HistoryItem ? _history : _plan;
}
