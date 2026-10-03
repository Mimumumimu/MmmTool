using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmTool.Core.Entities;
using MmmTool.Interop;
using MmmTool.ViewModels;
using Windows.Graphics;
using Windows.UI.Text;

namespace MmmTool.Views;

/// <summary>リマインダーのメイン画面</summary>
/// <remarks>
/// トレイメニュー・通知から開く普通のウィンドウ（モーダルではない）。アプリ内で 1 枚だけで、開いていれば前面に出す（<see cref="Services.ReminderWindowService"/>）。
/// 閉じたら破棄する（閉じたウィンドウは再表示できないため、次は作り直す）。大きさは変えられるが保存はせず、開くたびに最初の大きさに戻る。
/// 最大化・最小化はできない。タイトル帯をドラッグして移動できる。
/// </remarks>
public sealed partial class ReminderMainWindow : Window
{
    /// <summary>最初の幅（DIP）</summary>
    private const double InitialWidth = 360;
    /// <summary>最初の高さ（DIP）</summary>
    private const double InitialHeight = 440;
    /// <summary>最小の幅（DIP）。最初の幅より狭いと、状態の切り替えに押されて件名が見えなくなるため</summary>
    private const double MinimumWidth = 360;
    /// <summary>最小の高さ（DIP）</summary>
    private const double MinimumHeight = 320;

    /// <summary>ウィンドウの ViewModel</summary>
    public ReminderMainViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    public ReminderMainWindow(ReminderMainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);
        Closed += (_, _) => ViewModel.Dispose();
    }

    /// <summary>読み込んでから、主モニターの作業領域の中央に表示する</summary>
    /// <returns>読み込みと表示の完了を表すタスク</returns>
    public async Task ShowAsync()
    {
        // 空の画面が一瞬見えないよう、読み込んでから出す
        await ViewModel.InitializeAsync();

        // 大きさを決める倍率は、置くモニターのものを使うため、先にそのモニターへ移す
        var workArea = DisplayArea.Primary.WorkArea;
        AppWindow.Move(new PointInt32(workArea.X, workArea.Y));
        var scale = NativeMethods.GetDpiForWindow(Win32Interop.GetWindowFromWindowId(AppWindow.Id)) / 96.0;

        var presenter = OverlappedPresenter.Create();
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.PreferredMinimumWidth = (int)(MinimumWidth * scale);
        presenter.PreferredMinimumHeight = (int)(MinimumHeight * scale);
        AppWindow.SetPresenter(presenter);
        AppWindow.ResizeClient(new SizeInt32((int)(InitialWidth * scale), (int)(InitialHeight * scale)));

        var size = AppWindow.Size;
        AppWindow.Move(new PointInt32(
            workArea.X + Math.Max(0, (workArea.Width - size.Width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - size.Height) / 2)));
        Activate();
    }

    /// <summary>前面に出す（最小化していれば元に戻す）</summary>
    public void BringToFront()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }
        Activate();
        NativeMethods.SetForegroundWindow(Win32Interop.GetWindowFromWindowId(AppWindow.Id));
    }

    /// <summary>未対応の色（グレー）</summary>
    private static readonly SolidColorBrush NoneBrush = new(ColorHelper.FromArgb(0xFF, 0x9E, 0x9E, 0x9E));
    /// <summary>スヌーズの色（アンバー）</summary>
    private static readonly SolidColorBrush SnoozeBrush = new(ColorHelper.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
    /// <summary>完了の色（エメラルド系のグリーン）</summary>
    /// <remarks>参考値（#2E7D32）は暗くくすんでいてアンバーと釣り合わず、ダークテーマで沈むため、明るめにした（ユーザー決定）。</remarks>
    private static readonly SolidColorBrush DoneBrush = new(ColorHelper.FromArgb(0xFF, 0x16, 0xA3, 0x4A));

    /// <summary>状態の色（未＝グレー / スヌーズ＝アンバー / 完了＝グリーン）</summary>
    /// <param name="status">対応状態</param>
    /// <returns>状態を表すブラシ</returns>
    /// <remarks>ライト・ダークのどちらでも見分けやすい不透明の固定色。</remarks>
    public static Brush StatusBrush(ReminderStatus status) => status switch
    {
        ReminderStatus.Snooze => SnoozeBrush,
        ReminderStatus.Done => DoneBrush,
        _ => NoneBrush,
    };

    /// <summary>完了の件名は淡くする</summary>
    /// <param name="isDone">完了か</param>
    /// <returns>件名の不透明度</returns>
    public static double DoneOpacity(bool isDone) => isDone ? 0.5 : 1.0;

    /// <summary>完了の件名は取り消し線を引く</summary>
    /// <param name="isDone">完了か</param>
    /// <returns>文字の装飾</returns>
    public static TextDecorations Strike(bool isDone) => isDone ? TextDecorations.Strikethrough : TextDecorations.None;

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

    /// <summary>エラーを閉じたら消す（同じエラーがまた起きたときに出し直せるように）</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnErrorCloseClick(InfoBar sender, object args) => ViewModel.ErrorMessage = null;

    /// <summary>押された要素の行</summary>
    /// <param name="sender">メニュー項目</param>
    /// <returns>メニュー項目の行</returns>
    /// <remarks>メニューは画面の要素の外に出るので DataContext が受け継がれない。行は Tag に入れてある。</remarks>
    private static ReminderTodayItem ItemOf(object sender) => (ReminderTodayItem)((FrameworkElement)sender).Tag;
}
