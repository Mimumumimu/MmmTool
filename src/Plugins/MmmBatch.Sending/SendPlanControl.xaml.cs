using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace MmmBatch.Sending;

/// <summary>今日の送信予定の一覧の中身 (見出しつきの一覧・エラー)</summary>
/// <remarks>行を右クリックして、「再送」(まだ送っていないものは「今すぐ送る」)を選べる。</remarks>
public sealed partial class SendPlanControl : UserControl
{
    /// <summary>一覧の ViewModel</summary>
    public SendPlanViewModel ViewModel { get; }

    /// <summary>一覧の中身を作る</summary>
    /// <param name="viewModel">一覧の ViewModel</param>
    public SendPlanControl(SendPlanViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // 右クリック (メニューキー)した行を選択してからメニューを出す。行がメニューを出すときに処理済みにするので、処理済みでも受け取る
        PlanList.AddHandler(UIElement.RightTappedEvent, new RightTappedEventHandler((_, e) => SelectRowOf(e.OriginalSource)), handledEventsToo: true);
        PlanList.AddHandler(UIElement.ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>((_, e) => SelectRowOf(e.OriginalSource)), handledEventsToo: true);
    }

    /// <summary>メニュー「再送」「今すぐ送る」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnResendClick(object sender, RoutedEventArgs e)
        => await ViewModel.ResendCommand.ExecuteAsync((SendPlanItemViewModel)((FrameworkElement)sender).Tag);

    /// <summary>押された要素の行を選択する</summary>
    /// <param name="originalSource">押された要素</param>
    private void SelectRowOf(object originalSource)
    {
        if (originalSource is FrameworkElement { DataContext: SendPlanItemViewModel item })
        {
            PlanList.SelectedItem = item;
        }
    }
}
