using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.Reminders.BulkInput;

/// <summary>まとめて追加ウィンドウ (時刻の表で会議を選び、リマインダーを複数件登録する)</summary>
/// <remarks>
/// 親ウィンドウの上に、閉じるまで親を操作できないモーダルで出す。大きさは固定で、サイズ変更・最大化・最小化はできない。
/// タイトル帯をドラッグして移動できる。× で閉じたときはキャンセルと同じ。閉じたウィンドウは再表示できないので、開くたびに作り直す。
/// </remarks>
public sealed partial class ReminderBulkInputWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 960;

    /// <summary>ウィンドウの高さ (DIP)</summary>
    /// <remarks>タイトルバーを含む。時刻の表に 9 行ほどが見える高さ。</remarks>
    private const double WindowHeight = 640;

    /// <summary>タイトルバーの高さ (DIP)</summary>
    /// <remarks>ResizeClient は、タイトルバーを自分で描いていても、この分を上に足した大きさにする。</remarks>
    private const double TitleBarHeight = 32;

    /// <summary>閉じたときに結果を返す</summary>
    private readonly TaskCompletionSource<int> _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>登録した件数。キャンセル・× なら 0</summary>
    private int _result;

    /// <summary>ウィンドウの ViewModel</summary>
    public ReminderBulkInputViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public ReminderBulkInputWindow(ReminderBulkInputViewModel viewModel, AppEnvironment environment)
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
    /// <returns>登録した件数。キャンセル・× なら 0</returns>
    public async Task<int> ShowModalAsync(Window owner)
    {
        // すでにあるリマインダーを読んでから出す (同じ予定を重ねて追加しないため)
        await ViewModel.LoadAsync();

        _modal.SetOwner(owner);
        this.UseFixedPresenter(isDialog: true, isResizable: false);
        this.ResizeClientDip(WindowWidth, WindowHeight - TitleBarHeight, _modal.OwnerScale, roundUp: true);
        _modal.CenterOnOwner();

        _modal.Show();
        return await _closed.Task;
    }

    /// <summary>開いたら、時刻の表を今の時刻の行までスクロールする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>今の時刻に一番近い行を、表の上端に置く。これから入れる会議は先の時刻なので、先の行が多く見えるようにするため。</remarks>
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        var rowHeight = (double)RootGrid.Resources["SlotRowHeight"];
        TableScroll.UpdateLayout();
        TableScroll.ChangeView(null, ViewModel.InitialHour * rowHeight, null, disableAnimation: true);
    }

    /// <summary>件名にフォーカスが来たら IME をオンにする (日本語の入力が多いため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnJapaneseBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>リンクにフォーカスが来たら IME をオフにする (URL・パスを打つため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnLinkBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOff();

    /// <summary>登録・キャンセルで閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="count">登録した件数。キャンセルなら 0</param>
    private void OnCloseRequested(object? sender, int count)
    {
        _result = count;
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
