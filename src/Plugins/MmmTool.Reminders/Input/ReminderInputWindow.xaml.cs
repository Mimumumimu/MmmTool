using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using MmmTool.Reminders.Core;
using Windows.Foundation;

namespace MmmTool.Reminders.Input;

/// <summary>リマインダー入力ウィンドウ (1 件の新規登録・編集)</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。幅も高さも固定で、どのモードでも同じ大きさ。サイズ変更・最大化・最小化はできない。
/// タイトル帯をドラッグして移動できる。× で閉じたときはキャンセルと同じ。開くたびに作り直す。閉じたウィンドウは再表示できないため。
/// </remarks>
public sealed partial class ReminderInputWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    /// <remarks>高さとの比が、黄金比 (約 1.6 対 1)に近くなる値。</remarks>
    private const double WindowWidth = 860;

    /// <summary>ウィンドウの高さ (DIP)</summary>
    /// <remarks>タイトルバーを含む。右の列が、宛先と送信先の欄まで全部出たときの高さに合わせる。宛先・送信先が出ないモードでは、備考の欄が、その分だけ高くなる。</remarks>
    private const double WindowHeight = 540;

    /// <summary>備考の欄の最小の行数</summary>
    /// <remarks>備考の欄の最小の高さは、この行数ぶんの高さに、上下の余白と枠を足して決める。<see cref="OnRootLoaded"/> で行う。</remarks>
    private const int NoteMinLines = 3;

    /// <summary>左の列の幅 (DIP)</summary>
    /// <remarks>ウィンドウの幅から、左右の余白 24 ずつと、列の間隔 24 を引き、2 で割った値。日付と曜日の欄の高さを測るために使う。</remarks>
    private const double LeftColumnWidth = (WindowWidth - 24 * 2 - 24) / 2;

    /// <summary>タイトルバーの高さ (DIP)</summary>
    /// <remarks>ResizeClient は、タイトルバーを自分で描いていても、この分を上に足した大きさにする。</remarks>
    private const double TitleBarHeight = 32;

    /// <summary>閉じたときに結果を返す</summary>
    private readonly TaskCompletionSource<Reminder?> _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>保存した内容。キャンセル・× なら null</summary>
    private Reminder? _result;

    /// <summary>ウィンドウの ViewModel</summary>
    public ReminderInputViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public ReminderInputWindow(ReminderInputViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);

        ViewModel.CloseRequested += OnCloseRequested;
        RootGrid.Loaded += OnRootLoaded;
        Closed += OnClosed;
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <param name="target">編集するリマインダー。新規なら null (コピーして新規追加のときは連番 0 の内容)</param>
    /// <param name="copySourceNo">コピーして新規追加のときの、コピー元の連番。それ以外は 0</param>
    /// <returns>保存した内容。キャンセル・× なら null</returns>
    public async Task<Reminder?> ShowModalAsync(Window owner, Reminder? target, int copySourceNo = 0)
    {
        ViewModel.Load(target);
        // 宛先の欄が空のまま見えないよう、選択肢を作ってから出す
        await ViewModel.LoadTargetsAsync();
        await ViewModel.LoadChannelsAsync(copySourceNo);

        _modal.SetOwner(owner);
        this.UseFixedPresenter(isDialog: true, isResizable: false);

        this.ResizeClientDip(WindowWidth, WindowHeight - TitleBarHeight, _modal.OwnerScale, roundUp: true);
        _modal.CenterOnOwner();

        _modal.Show();
        return await _closed.Task;
    }

    /// <summary>日付指定・曜日指定の欄の高さを決める</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 高いほうに合わせた高さを確保しておく。切り替えで下の欄やボタンが動かないようにするため。
    /// そのために一度だけ両方を表示して測り、表示はバインドで元に戻す。
    /// あわせて、備考の最小の高さを決め、中身が <see cref="WindowHeight"/> に収まらないときだけ、ウィンドウを中身の高さまで広げる。
    /// </remarks>
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        DatePanel.Visibility = Visibility.Visible;
        WeekdayPanel.Visibility = Visibility.Visible;
        DatePanel.Measure(new Size(LeftColumnWidth, double.PositiveInfinity));
        WeekdayPanel.Measure(new Size(LeftColumnWidth, double.PositiveInfinity));
        DateArea.MinHeight = Math.Max(DatePanel.DesiredSize.Height, WeekdayPanel.DesiredSize.Height);
        Bindings.Update();

        // 備考の最小の高さ = 行数 x 1 行の高さ + 上下の余白 + 枠。1 行の高さは、入力欄と同じフォントの 1 行を測って求める
        var probe = new TextBlock { Text = "あ", FontFamily = NoteBox.FontFamily, FontSize = NoteBox.FontSize };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        NoteBox.MinHeight = (probe.DesiredSize.Height * NoteMinLines)
            + NoteBox.Padding.Top + NoteBox.Padding.Bottom
            + NoteBox.BorderThickness.Top + NoteBox.BorderThickness.Bottom;

        // 文字を大きくしたときに、中身が切れないようにする。固定の高さに収まるときは、そのままにする
        // 備考は余りを受け持つので、測った高さは、備考が最小のときの高さになる
        RootGrid.Measure(new Size(WindowWidth, double.PositiveInfinity));
        if (RootGrid.DesiredSize.Height > WindowHeight && this.ResizeToContentHeight(RootGrid, WindowWidth))
        {
            _modal.CenterOnOwner();
        }
    }

    /// <summary>件名・備考にフォーカスが来たら IME をオンにする (日本語の入力が多いため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnJapaneseBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>リンクにフォーカスが来たら IME をオフにする (URL・パスを打つため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnLinkBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOff();

    /// <summary>保存・キャンセルで閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="saved">保存した内容。キャンセルなら null</param>
    private void OnCloseRequested(object? sender, Reminder? saved)
    {
        _result = saved;
        _modal.Close();
    }

    /// <summary>閉じたら結果を返す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        _closed.TrySetResult(_result);
    }
}
