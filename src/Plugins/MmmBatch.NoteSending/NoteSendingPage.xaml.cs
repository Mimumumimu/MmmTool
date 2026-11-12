using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Utilities;

namespace MmmBatch.NoteSending;

/// <summary>「ノートの送信」のページ</summary>
/// <remarks>
/// 起動してから送った記録の履歴だけを出す。
/// 状態を DB に持たないので、DB から読む「最近の依頼」と、送り直す「再送」は無い。
/// ページを出すたびに一覧を読み直す。
/// 出していない間に送った分をすぐ見せるため。
/// </remarks>
public sealed partial class NoteSendingPage : Page
{
    /// <summary>送信の履歴の画面</summary>
    private readonly NoteHistoryControl _history;

    /// <summary>ページを作る</summary>
    /// <param name="history">送信の履歴の画面</param>
    public NoteSendingPage(NoteHistoryControl history)
    {
        _history = history;
        InitializeComponent();
        ContentHost.Content = history;
        Loaded += OnLoaded;
    }

    /// <summary>ページを出したら一覧を読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnLoaded(object sender, RoutedEventArgs e) => _history.ViewModel.RefreshAsync().Forget();
}
