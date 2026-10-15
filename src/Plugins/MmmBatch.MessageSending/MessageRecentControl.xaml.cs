using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace MmmBatch.MessageSending;

/// <summary>最近の依頼の一覧の中身 (見出しつきの一覧・エラー)</summary>
/// <remarks>行を右クリックして、「再送」(まだ送っていないものは「今すぐ送る」)を選べる。</remarks>
public sealed partial class MessageRecentControl : UserControl
{
    /// <summary>一覧の ViewModel</summary>
    public MessageRecentViewModel ViewModel { get; }

    /// <summary>一覧の中身を作る</summary>
    /// <param name="viewModel">一覧の ViewModel</param>
    public MessageRecentControl(MessageRecentViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // 右クリック (メニューキー)した行を選択してからメニューを出す。行がメニューを出すときに処理済みにするので、処理済みでも受け取る
        RecentList.AddHandler(UIElement.RightTappedEvent, new RightTappedEventHandler((_, e) => SelectRowOf(e.OriginalSource)), handledEventsToo: true);
        RecentList.AddHandler(UIElement.ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>((_, e) => SelectRowOf(e.OriginalSource)), handledEventsToo: true);
    }

    /// <summary>メニュー「再送」「今すぐ送る」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnResendClick(object sender, RoutedEventArgs e)
        => await ViewModel.ResendCommand.ExecuteAsync((MessageRecentItemViewModel)((FrameworkElement)sender).Tag);

    /// <summary>押された要素の行を選択する</summary>
    /// <param name="originalSource">押された要素</param>
    private void SelectRowOf(object originalSource)
    {
        if (originalSource is FrameworkElement { DataContext: MessageRecentItemViewModel item })
        {
            RecentList.SelectedItem = item;
        }
    }
}
