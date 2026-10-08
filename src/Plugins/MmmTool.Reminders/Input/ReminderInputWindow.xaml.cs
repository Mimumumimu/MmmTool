using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using MmmTool.Reminders.Core;
using Windows.Foundation;

namespace MmmTool.Reminders.Input;

/// <summary>リマインダー入力ウィンドウ (1 件の新規登録・編集)</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。幅は固定で、高さも固定 (サイズ変更・最大化・最小化はできない)。
/// タイトル帯をドラッグして移動できる。× で閉じたときはキャンセルと同じ。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// </remarks>
public sealed partial class ReminderInputWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 480;

    /// <summary>ウィンドウの高さの見積もり (DIP。タイトルバーを含む。宛先の欄が出る場合)</summary>
    /// <remarks>表示する前の仮の高さ。表示して中身が載ったら、中身の高さを測って、ちょうど合わせ直す (<see cref="OnRootLoaded"/>)。送信先の欄が出るときは、<see cref="ChannelFieldHeight"/> を足す。</remarks>
    private const double WindowHeight = 724;

    /// <summary>送信先の欄の高さの見積もり (DIP。見出し + 選択欄 + 間隔)</summary>
    /// <remarks>送信先の欄が出るとき (DB モードで、送信先を読み込めたとき)だけ、<see cref="WindowHeight"/> に足す。表示前の仮の高さのための数字で、最終の高さは、中身を測って決める。</remarks>
    private const double ChannelFieldHeight = 76;

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
    /// <returns>保存した内容。キャンセル・× なら null</returns>
    public async Task<Reminder?> ShowModalAsync(Window owner, Reminder? target)
    {
        ViewModel.Load(target);
        // 宛先の欄が空のまま見えないよう、選択肢を作ってから出す
        await ViewModel.LoadTargetsAsync();
        await ViewModel.LoadChannelsAsync();

        _modal.SetOwner(owner);
        this.UseFixedPresenter(isDialog: true, isResizable: false);

        var height = WindowHeight + (ViewModel.IsChannelVisible ? ChannelFieldHeight : 0);
        this.ResizeClientDip(WindowWidth, height - TitleBarHeight, _modal.OwnerScale, roundUp: true);
        _modal.CenterOnOwner();

        _modal.Show();
        return await _closed.Task;
    }

    /// <summary>日付指定・曜日指定の欄の高さを決める</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 高いほうに合わせた高さを確保しておく (切り替えで下の欄やボタンが動かないように)。
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

        // ウィンドウの高さを、中身の高さにちょうど合わせる (欄の出入り・文字の大きさ・画面の倍率で、見積もりの数字とずれても、切れない)
        if (this.ResizeToContentHeight(RootGrid, WindowWidth))
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
