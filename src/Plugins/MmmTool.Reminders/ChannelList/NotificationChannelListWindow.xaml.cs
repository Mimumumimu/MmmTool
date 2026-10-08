using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using Windows.Foundation;
using Windows.Graphics;

namespace MmmTool.Reminders.ChannelList;

/// <summary>送信先の一覧ウィンドウ</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。大きさは変えられる (最大化・最小化はできない)。
/// タイトル帯をドラッグして移動できる。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// 行のダブルタップで編集、右クリックで行のメニュー (編集・削除)。
/// </remarks>
public sealed partial class NotificationChannelListWindow : Window
{
    /// <summary>最初の幅 (DIP)</summary>
    private const double InitialWidth = 560;
    /// <summary>最初の高さ (DIP)</summary>
    private const double InitialHeight = 420;
    /// <summary>最小の幅 (DIP)</summary>
    private const double MinimumWidth = 440;
    /// <summary>最小の高さ (DIP)</summary>
    private const double MinimumHeight = 320;

    /// <summary>閉じたら完了する</summary>
    private readonly TaskCompletionSource _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>ウィンドウの ViewModel</summary>
    public NotificationChannelListViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public NotificationChannelListWindow(NotificationChannelListViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);

        // 右クリック (メニューキー)した行を選択してからメニューを出す。行がメニューを出すときに処理済みにするので、処理済みでも受け取る
        ChannelListView.AddHandler(UIElement.RightTappedEvent, new RightTappedEventHandler((_, e) => SelectRowOf(e.OriginalSource)), handledEventsToo: true);
        ChannelListView.AddHandler(UIElement.ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>((_, e) => SelectRowOf(e.OriginalSource)), handledEventsToo: true);

        Closed += OnClosed;
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <returns>閉じるまでの待機を表すタスク</returns>
    public async Task ShowModalAsync(Window owner)
    {
        // 空の一覧が一瞬見えないよう、読み込んでから出す
        await ViewModel.InitializeAsync();

        _modal.SetOwner(owner);
        var scale = _modal.OwnerScale;
        this.UseFixedPresenter(isDialog: true, isResizable: true, new SizeInt32((int)(MinimumWidth * scale), (int)(MinimumHeight * scale)));
        this.ResizeClientDip(InitialWidth, InitialHeight, scale);
        _modal.CenterOnOwner();

        _modal.Show();
        await _closed.Task;
    }

    /// <summary>行のダブルタップで編集</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ダブルタップの情報</param>
    private async void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is NotificationChannelItem item)
        {
            await ViewModel.EditCommand.ExecuteAsync(item);
        }
    }

    /// <summary>メニュー「編集」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnEditClick(object sender, RoutedEventArgs e) => await ViewModel.EditCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>メニュー「削除」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnDeleteClick(object sender, RoutedEventArgs e) => await ViewModel.DeleteCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>押された要素の行を選択する</summary>
    /// <param name="originalSource">押された要素</param>
    private void SelectRowOf(object originalSource)
    {
        if (originalSource is FrameworkElement { DataContext: NotificationChannelItem item })
        {
            ChannelListView.SelectedItem = item;
        }
    }

    /// <summary>メニュー項目の行</summary>
    /// <param name="sender">メニュー項目</param>
    /// <returns>メニュー項目の行</returns>
    /// <remarks>メニューは画面の要素の外に出るので DataContext が受け継がれない。行は Tag に入れてある。</remarks>
    private static NotificationChannelItem ItemOf(object sender) => (NotificationChannelItem)((FrameworkElement)sender).Tag;

    /// <summary>閉じたら完了を返す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args) => _closed.TrySetResult();
}
