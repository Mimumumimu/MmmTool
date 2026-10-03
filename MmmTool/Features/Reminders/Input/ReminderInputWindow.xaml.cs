using Microsoft.UI.Xaml;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using MmmTool.Core.Reminders;
using MmmTool.Shell;
using Windows.Foundation;

namespace MmmTool.Features.Reminders.Input;

/// <summary>リマインダー入力ウィンドウ（1 件の新規登録・編集）</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル（閉じるまで親を操作できない）で出す。幅は固定で、高さは中身に合わせて決めたら固定（サイズ変更・最大化・最小化はできない）。
/// タイトル帯をドラッグして移動できる。× で閉じたときはキャンセルと同じ。開くたびに作り直す（閉じたウィンドウは再表示できないため）。
/// </remarks>
public sealed partial class ReminderInputWindow : Window
{
    /// <summary>ウィンドウの幅（DIP）</summary>
    private const double WindowWidth = 480;

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
    public ReminderInputWindow(ReminderInputViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, AppIcon.FilePath);
        _modal = new PseudoModal(this);

        ViewModel.CloseRequested += OnCloseRequested;
        RootGrid.Loaded += OnRootLoaded;
        Closed += OnClosed;
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <param name="target">編集するリマインダー。新規なら null（コピーして新規追加のときは連番 0 の内容）</param>
    /// <returns>保存した内容。キャンセル・× なら null</returns>
    public Task<Reminder?> ShowModalAsync(Window owner, Reminder? target)
    {
        ViewModel.Load(target);

        _modal.SetOwner(owner);
        this.UseFixedPresenter(isDialog: true, isResizable: false);

        // 高さは中身を読み込んでから決め直す（OnRootLoaded）。ここでは仮の大きさで親の中央に置く
        var scale = _modal.OwnerScale;
        this.ResizeClientDip(WindowWidth, 640, scale);
        _modal.CenterOnOwner();

        _modal.Show();
        return _closed.Task;
    }

    /// <summary>中身に合わせて高さを決め、親の中央に置き直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 日付指定・曜日指定の欄は、高いほうに合わせた高さを確保しておく（切り替えで下の欄やボタンが動かないように）。
    /// そのために一度だけ両方を表示して測り、表示はバインドで元に戻す。
    /// </remarks>
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        DatePanel.Visibility = Visibility.Visible;
        WeekdayPanel.Visibility = Visibility.Visible;
        DatePanel.Measure(new Size(WindowWidth, double.PositiveInfinity));
        WeekdayPanel.Measure(new Size(WindowWidth, double.PositiveInfinity));
        DateArea.MinHeight = Math.Max(DatePanel.DesiredSize.Height, WeekdayPanel.DesiredSize.Height);
        Bindings.Update();

        RootGrid.Measure(new Size(WindowWidth, double.PositiveInfinity));
        var scale = RootGrid.XamlRoot.RasterizationScale;
        this.ResizeClientDip(WindowWidth, RootGrid.DesiredSize.Height, scale, roundUp: true);
        _modal.CenterOnOwner();
    }

    /// <summary>件名・備考にフォーカスが来たら IME をオンにする（日本語の入力が多いため）</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnJapaneseBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>リンクにフォーカスが来たら IME をオフにする（URL・パスを打つため）</summary>
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
