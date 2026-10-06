using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.WindowPositions;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using MmmTool.Core.Reminders;
using MmmTool.Shell;
using Windows.Graphics;

namespace MmmTool.Features.Reminders.Main;

/// <summary>リマインダーのメイン画面</summary>
/// <remarks>
/// トレイメニュー・通知から開く普通のウィンドウ (モーダルではない)。アプリ内で 1 枚だけで、開いていれば前面に出す (<see cref="ReminderWindowService"/>)。
/// 閉じたら破棄する (閉じたウィンドウは再表示できないため、次は作り直す)。位置と大きさは SDK の <see cref="WindowBoundsKeeper"/> が保存し、次に開くとき復元する。
/// 保存が無い・画面外のときは、最初の大きさで主モニターの作業領域の右下に出す。最大化・最小化はできない。タイトル帯をドラッグして移動できる。
/// </remarks>
public sealed partial class ReminderMainWindow : Window
{
    /// <summary>最初の幅 (DIP)</summary>
    private const double InitialWidth = 360;
    /// <summary>最初の高さ (DIP)</summary>
    private const double InitialHeight = 440;
    /// <summary>最小の幅 (DIP)。最初の幅より狭いと、状態の切り替えに押されて件名が見えなくなるため</summary>
    private const double MinimumWidth = 360;
    /// <summary>最小の高さ (DIP)</summary>
    private const double MinimumHeight = 320;

    /// <summary>ウィンドウの位置と大きさの保存・復元</summary>
    private readonly IWindowPositionService _positions;

    /// <summary>ウィンドウの ViewModel</summary>
    public ReminderMainViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="positions">ウィンドウの位置と大きさの保存・復元</param>
    public ReminderMainWindow(ReminderMainViewModel viewModel, IWindowPositionService positions)
    {
        ViewModel = viewModel;
        _positions = positions;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, AppIcon.FilePath);
        Closed += (_, _) => ViewModel.Dispose();
    }

    /// <summary>読み込んでから、前回の位置と大きさで表示する</summary>
    /// <returns>読み込みと表示の完了を表すタスク</returns>
    public async Task ShowAsync()
    {
        // 空の画面が一瞬見えないよう、読み込んでから出す
        await ViewModel.InitializeAsync();

        // 最小の大きさの倍率は、置くモニターのものを使うため、位置を復元してから求める
        WindowBoundsKeeper.Attach(this, _positions, "ReminderMainWindow", InitialWidth, InitialHeight, DefaultWindowPlacement.PrimaryBottomRight);
        var scale = this.GetDpiScale();

        this.UseFixedPresenter(isDialog: false, isResizable: true, new SizeInt32((int)(MinimumWidth * scale), (int)(MinimumHeight * scale)));
        Activate();
    }

    /// <summary>未対応の色 (グレー)</summary>
    private static readonly SolidColorBrush NoneBrush = new(ColorHelper.FromArgb(0xFF, 0x9E, 0x9E, 0x9E));
    /// <summary>スヌーズの色 (アンバー)</summary>
    private static readonly SolidColorBrush SnoozeBrush = new(ColorHelper.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
    /// <summary>完了の色 (エメラルド系のグリーン)</summary>
    /// <remarks>濃い緑 (#2E7D32 など)は暗くくすんでアンバーと釣り合わず、ダークテーマで沈むため、明るめにした。</remarks>
    private static readonly SolidColorBrush DoneBrush = new(ColorHelper.FromArgb(0xFF, 0x16, 0xA3, 0x4A));

    /// <summary>状態の色 (未＝グレー / スヌーズ＝アンバー / 完了＝グリーン)</summary>
    /// <param name="status">対応状態</param>
    /// <returns>状態を表すブラシ</returns>
    /// <remarks>ライト・ダークのどちらでも見分けやすい不透明の固定色。</remarks>
    public static Brush StatusBrush(ReminderStatus status) => status switch
    {
        ReminderStatus.Snooze => SnoozeBrush,
        ReminderStatus.Done => DoneBrush,
        _ => NoneBrush,
    };

    /// <summary>件名を押したら、リンクがあれば開く</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">タップの情報</param>
    private async void OnTitleTapped(object sender, TappedRoutedEventArgs e)
    {
        var item = ItemOf(sender);
        if (item.HasLink)
        {
            await ViewModel.OpenLinkCommand.ExecuteAsync(item);
        }
    }

    /// <summary>メニュー「リンクを開く」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnOpenLinkClick(object sender, RoutedEventArgs e) => await ViewModel.OpenLinkCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>メニュー「編集」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnEditClick(object sender, RoutedEventArgs e) => await ViewModel.EditCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>メニュー「削除」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnDeleteClick(object sender, RoutedEventArgs e) => await ViewModel.DeleteCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>押された要素の行</summary>
    /// <param name="sender">メニュー項目</param>
    /// <returns>メニュー項目の行</returns>
    /// <remarks>メニューは画面の要素の外に出るので DataContext が受け継がれない。行は Tag に入れてある。</remarks>
    private static ReminderTodayItem ItemOf(object sender) => (ReminderTodayItem)((FrameworkElement)sender).Tag;
}
