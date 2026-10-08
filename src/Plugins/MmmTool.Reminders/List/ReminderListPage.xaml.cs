using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Reminders.List;

/// <summary>リマインダー一覧ページ (サイドバーの「リマインダー」)</summary>
/// <remarks>
/// 一覧の中身は <see cref="ReminderListControl"/> (ウィンドウと共有)。
/// ページは作ったあと使い回されるので、「過去の予定を表示」「削除済みを表示」の状態は、ページを切り替えても残る。
/// </remarks>
public sealed partial class ReminderListPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public ReminderListViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public ReminderListPage(ReminderListViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ListHost.Child = new ReminderListControl(viewModel);
    }

    /// <summary>表示のたびに読み直し、最初の読み込みが済んだら一覧を出す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>空の一覧が一瞬見えないよう、最初の読み込みが済むまで隠す (ウィンドウと同じ)。</remarks>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        ListHost.Visibility = Visibility.Visible;
    }
}
