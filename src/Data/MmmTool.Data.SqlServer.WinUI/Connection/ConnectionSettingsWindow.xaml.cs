using System.ComponentModel;
using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.Data.SqlServer.WinUI.Connection;

/// <summary>DB の接続の設定ウィンドウ</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。幅は固定で、高さは中身に合わせて決める (お知らせが出入りしたら、決め直す)。
/// サイズ変更・最大化・最小化はできない。保存・キャンセル・× のどれでも閉じる。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// </remarks>
public sealed partial class ConnectionSettingsWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 480;

    /// <summary>閉じたことを知らせる</summary>
    private readonly TaskCompletionSource _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>ウィンドウの ViewModel</summary>
    public ConnectionSettingsViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public ConnectionSettingsWindow(ConnectionSettingsViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ConnectionHost.Content = new DatabaseConnectionForm(viewModel.Connection);
        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);

        ViewModel.CloseRequested += OnCloseRequested;
        ViewModel.Connection.PropertyChanged += OnConnectionPropertyChanged;
        ViewModel.Connection.Error.PropertyChanged += OnConnectionPropertyChanged;
        ViewModel.Connection.Success.PropertyChanged += OnConnectionPropertyChanged;
        RootGrid.Loaded += OnRootLoaded;
        Closed += OnClosed;
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <returns>ウィンドウが閉じたことを表すタスク</returns>
    public async Task ShowModalAsync(Window owner)
    {
        _modal.SetOwner(owner);
        this.UseFixedPresenter(isDialog: true, isResizable: false);

        // 高さは中身を読み込んでから決め直す (OnRootLoaded)。ここでは仮の大きさで親の中央に置く
        this.ResizeClientDip(WindowWidth, 440, _modal.OwnerScale);
        _modal.CenterOnOwner();

        _modal.Show();
        await _closed.Task;
    }

    /// <summary>中身に合わせて高さを決める</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRootLoaded(object sender, RoutedEventArgs e) => FitHeight();

    /// <summary>証明書の確認・エラー・成功のお知らせが変わったら、中身の出入りに合わせて高さを決め直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変わったプロパティの情報</param>
    /// <remarks>お知らせの表示がバインドで切り替わったあとに測るため、UI スレッドの次の順番に回す。</remarks>
    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DatabaseConnectionViewModel.NeedsCertificateConsent) or nameof(ErrorState.IsOpen) or nameof(ErrorState.Message))
        {
            DispatcherQueue.TryEnqueue(FitHeight);
        }
    }

    /// <summary>中身の高さに合わせて、ウィンドウの大きさを決め、親の中央に置き直す</summary>
    private void FitHeight()
    {
        if (this.ResizeToContentHeight(RootGrid, WindowWidth))
        {
            _modal.CenterOnOwner();
        }
    }

    /// <summary>保存・キャンセルで閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnCloseRequested(object? sender, EventArgs e) => _modal.Close();

    /// <summary>閉じたら、待っている側へ知らせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        ViewModel.Connection.PropertyChanged -= OnConnectionPropertyChanged;
        ViewModel.Connection.Error.PropertyChanged -= OnConnectionPropertyChanged;
        ViewModel.Connection.Success.PropertyChanged -= OnConnectionPropertyChanged;
        _closed.TrySetResult();
    }
}
