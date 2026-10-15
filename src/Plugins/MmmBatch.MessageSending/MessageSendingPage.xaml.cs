using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Utilities;

namespace MmmBatch.MessageSending;

/// <summary>「メッセージの送信」のページ (「最近の依頼」と、「履歴」を切り替える)</summary>
/// <remarks>
/// 最初は「最近の依頼」を出す。2 つの画面は、作ったあと使い回す (切り替えのたびに作り直さない)。
/// ページを出すたびに、一覧を読み直す (出していない間に変わった分を、すぐ見せるため)。
/// </remarks>
public sealed partial class MessageSendingPage : Page
{
    /// <summary>最近の依頼の画面</summary>
    private readonly MessageRecentControl _recent;

    /// <summary>送信の履歴の画面</summary>
    private readonly MessageHistoryControl _history;

    /// <summary>ページを作る</summary>
    /// <param name="recent">最近の依頼の画面</param>
    /// <param name="history">送信の履歴の画面</param>
    public MessageSendingPage(MessageRecentControl recent, MessageHistoryControl history)
    {
        _recent = recent;
        _history = history;
        InitializeComponent();
        ContentHost.Content = recent;
        Loaded += OnLoaded;
    }

    /// <summary>ページを出したら、一覧を読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _recent.ViewModel.RefreshAsync().Forget();
        _history.ViewModel.RefreshAsync().Forget();
    }

    /// <summary>切り替えで、出す画面を替える</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">イベントの情報</param>
    private void OnSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        => ContentHost.Content = sender.SelectedItem == HistoryItem ? _history : _recent;
}
