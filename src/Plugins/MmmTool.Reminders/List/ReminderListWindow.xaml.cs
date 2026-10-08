using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using Windows.Graphics;

namespace MmmTool.Reminders.List;

/// <summary>リマインダー一覧ウィンドウ</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。大きさは変えられる (最大化・最小化はできない)。
/// タイトル帯をドラッグして移動できる。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// 一覧の中身は <see cref="ReminderListControl"/> (ページと共有)。
/// </remarks>
public sealed partial class ReminderListWindow : Window
{
    /// <summary>最初の幅 (DIP)</summary>
    private const double InitialWidth = 760;
    /// <summary>最初の高さ (DIP)</summary>
    private const double InitialHeight = 560;
    /// <summary>最小の幅 (DIP)。日付・曜日・時刻の列と件名が少し見える幅</summary>
    private const double MinimumWidth = 560;
    /// <summary>最小の高さ (DIP)</summary>
    private const double MinimumHeight = 360;

    /// <summary>閉じたら完了する</summary>
    private readonly TaskCompletionSource _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>ウィンドウの ViewModel</summary>
    public ReminderListViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public ReminderListWindow(ReminderListViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ListHost.Child = new ReminderListControl(viewModel);

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);

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

    /// <summary>閉じたら購読をやめて完了を返す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.Dispose();
        _closed.TrySetResult();
    }
}
