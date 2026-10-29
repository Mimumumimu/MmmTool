using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.WindowPositions;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using MmmTool.Reminders.Core;
using Windows.Graphics;

namespace MmmTool.Reminders.Main;

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

    /// <summary>備考のアイコン (メモ)</summary>
    private const string NoteGlyph = "";
    /// <summary>コピーしたことを知らせるアイコン (チェック)</summary>
    private const string CheckGlyph = "";
    /// <summary>チェックを出しておく時間</summary>
    private static readonly TimeSpan CopiedDisplayTime = TimeSpan.FromSeconds(1.5);

    /// <summary>ウィンドウの位置と大きさの保存・復元</summary>
    private readonly IWindowPositionService _positions;
    /// <summary>備考のコピーのチェックを、元のアイコンに戻すまで数えるタイマー</summary>
    private readonly DispatcherQueueTimer _copiedTimer;
    /// <summary>いまチェックを出している備考のアイコン。出していなければ null</summary>
    private FontIcon? _copiedIcon;

    /// <summary>ウィンドウの ViewModel</summary>
    public ReminderMainViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="positions">ウィンドウの位置と大きさの保存・復元</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public ReminderMainWindow(ReminderMainViewModel viewModel, IWindowPositionService positions, AppEnvironment environment)
    {
        ViewModel = viewModel;
        _positions = positions;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);

        _copiedTimer = DispatcherQueue.CreateTimer();
        _copiedTimer.Interval = CopiedDisplayTime;
        _copiedTimer.IsRepeating = false;
        _copiedTimer.Tick += OnCopiedTimerTick;

        Closed += (_, _) =>
        {
            _copiedTimer.Stop();
            _copiedTimer.Tick -= OnCopiedTimerTick;
            ViewModel.Dispose();
        };
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

    /// <summary>備考のアイコンを押したら、備考の全文をコピーして、アイコンを一瞬チェックにする</summary>
    /// <param name="sender">イベントの送信元 (備考のボタン)</param>
    /// <param name="e">イベントの情報</param>
    private void OnCopyNoteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyNoteCommand.Execute(ItemOf(sender));

        if (((Button)sender).Content is not FontIcon icon)
        {
            return;
        }

        // 続けて押されたときは、前のチェックを戻してから、時間を数え直す
        ResetCopiedIcon();
        icon.Glyph = CheckGlyph;
        _copiedIcon = icon;
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    /// <summary>チェックの時間が過ぎたら、アイコンを元のメモに戻す</summary>
    /// <param name="sender">タイマー</param>
    /// <param name="args">イベントの情報</param>
    private void OnCopiedTimerTick(DispatcherQueueTimer sender, object args) => ResetCopiedIcon();

    /// <summary>チェックを出している備考のアイコンを、元のメモに戻す</summary>
    private void ResetCopiedIcon()
    {
        if (_copiedIcon is { } icon)
        {
            icon.Glyph = NoteGlyph;
            _copiedIcon = null;
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

    /// <summary>メニュー「コピーして新規追加」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnCopyAsNewClick(object sender, RoutedEventArgs e) => await ViewModel.CopyAsNewCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>メニュー「削除」</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnDeleteClick(object sender, RoutedEventArgs e) => await ViewModel.DeleteCommand.ExecuteAsync(ItemOf(sender));

    /// <summary>押された要素の行</summary>
    /// <param name="sender">メニュー項目か、備考のボタン</param>
    /// <returns>押された要素の行</returns>
    /// <remarks>メニューは画面の要素の外に出るので DataContext が受け継がれない。行は、メニュー項目も備考のボタンも Tag に入れてある。</remarks>
    private static ReminderTodayItem ItemOf(object sender) => (ReminderTodayItem)((FrameworkElement)sender).Tag;
}
