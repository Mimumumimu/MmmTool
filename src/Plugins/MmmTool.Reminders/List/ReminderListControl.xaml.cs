using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace MmmTool.Reminders.List;

/// <summary>リマインダー一覧の中身 (操作行・見出しつきの一覧・エラー)</summary>
/// <remarks>
/// 一覧のページ (<see cref="ReminderListPage"/>)と、一覧のウィンドウ (<see cref="ReminderListWindow"/>)で共有する。
/// タイトル・外側の余白・読み込みは、置く側が受け持つ。
/// 行のダブルタップで編集、右クリックで行のメニュー (削除済みでない行は「削除」、削除済みの行は「完全削除」を出す)。
/// </remarks>
public sealed partial class ReminderListControl : UserControl
{
    /// <summary>一覧の ViewModel</summary>
    public ReminderListViewModel ViewModel { get; }

    /// <summary>一覧の中身を作る</summary>
    /// <param name="viewModel">一覧の ViewModel</param>
    public ReminderListControl(ReminderListViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // 右クリック (メニューキー)した行を選択してからメニューを出す。行がメニューを出すときに処理済みにするので、処理済みでも受け取る
        ReminderList.AddHandler(UIElement.RightTappedEvent, new RightTappedEventHandler((_, e) => SelectRowOf(e.OriginalSource)), handledEventsToo: true);
        ReminderList.AddHandler(UIElement.ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>((_, e) => SelectRowOf(e.OriginalSource)), handledEventsToo: true);
    }

    /// <summary>行のダブルタップで編集</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ダブルタップの情報</param>
    private async void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ReminderListItem item)
        {
            await ViewModel.EditCommand.ExecuteAsync(item);
        }
    }

    /// <summary>メニュー「編集」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnEditClick(object sender, RoutedEventArgs e) => await ViewModel.EditCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>メニュー「コピーして新規追加」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnCopyAsNewClick(object sender, RoutedEventArgs e) => await ViewModel.CopyAsNewCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>メニュー「削除」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnDeleteClick(object sender, RoutedEventArgs e) => await ViewModel.DeleteCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>メニュー「完全削除」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnPurgeClick(object sender, RoutedEventArgs e) => await ViewModel.PurgeCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>押された要素の行を選択する</summary>
    /// <param name="originalSource">押された要素</param>
    private void SelectRowOf(object originalSource)
    {
        if (originalSource is FrameworkElement { DataContext: ReminderListItem item })
        {
            ReminderList.SelectedItem = item;
        }
    }

    /// <summary>メニュー項目の行</summary>
    /// <param name="sender">メニュー項目</param>
    /// <returns>メニュー項目の行</returns>
    /// <remarks>メニューは画面の要素の外に出るので DataContext が受け継がれない。行は Tag に入れてある。</remarks>
    private static ReminderListItem ItemOf(object sender) => (ReminderListItem)((FrameworkElement)sender).Tag;
}
