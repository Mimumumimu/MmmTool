using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Utilities;
using MmmTool.Features.Database.Connection;
using Windows.Foundation;

namespace MmmTool.Features.Database.Choice;

/// <summary>初回の保存先の選択ウィンドウ</summary>
/// <remarks>
/// 親を持たない、独立したウィンドウ (起動のあと、メインウィンドウがまだ出ていないときに出すため)。主モニターの中央に出す。
/// 幅は固定で、高さは中身に合わせて決める (DB を選んで入力欄が出たら、決め直す)。サイズ変更・最大化・最小化はできない。
/// 「決定」では、保存したあと、画面を閉じずに隠す (最初のウィンドウなので、閉じるとアプリごと終了しうる。閉じるのは、呼び出し側が、メインウィンドウを作ったあとに行う)。
/// × で閉じたときは、保存しない (呼び出し側がアプリを終了する)。閉じたウィンドウは再表示できないので、作り直して使う。
/// </remarks>
public sealed partial class DatabaseChoiceWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 480;

    /// <summary>閉じたときに結果を返す</summary>
    private readonly TaskCompletionSource<bool> _closed = new();

    /// <summary>保存して決定したか。× なら false</summary>
    private bool _result;

    /// <summary>ウィンドウの ViewModel</summary>
    public DatabaseChoiceViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public DatabaseChoiceWindow(DatabaseChoiceViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ConnectionHost.Content = new DatabaseConnectionForm(viewModel.Connection);
        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        ViewModel.CloseRequested += OnCloseRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Connection.PropertyChanged += OnViewModelPropertyChanged;
        RootGrid.Loaded += OnRootLoaded;
        Closed += OnClosed;
    }

    /// <summary>主モニターの中央に表示し、選ぶ (または閉じる)まで待つ</summary>
    /// <returns>保存して決定したら true (画面は隠れるだけで、閉じていない)。× で閉じたら false</returns>
    public Task<bool> ShowAsync()
    {
        this.UseFixedPresenter(isDialog: true, isResizable: false);

        // 高さは中身を読み込んでから決め直す (OnRootLoaded)。ここでは仮の大きさで中央に置く
        this.ResizeClientDip(WindowWidth, 360, this.GetDpiScale());
        this.MoveCentered(DisplayArea.Primary.WorkArea);

        Activate();
        this.BringToFront();
        return _closed.Task;
    }

    /// <summary>中身に合わせて高さを決める</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRootLoaded(object sender, RoutedEventArgs e) => FitHeight();

    /// <summary>ローカル・DB の選択や、証明書の確認の表示が変わったら、中身の出入りに合わせて高さを決め直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変わったプロパティの情報</param>
    /// <remarks>入力欄の表示がバインドで切り替わったあとに測るため、UI スレッドの次の順番に回す。</remarks>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DatabaseChoiceViewModel.IsSqlServer) or nameof(DatabaseConnectionViewModel.NeedsCertificateConsent))
        {
            DispatcherQueue.TryEnqueue(FitHeight);
        }
    }

    /// <summary>中身の高さに合わせて、ウィンドウの大きさを決め、中央に置き直す</summary>
    private void FitHeight()
    {
        RootGrid.Measure(new Size(WindowWidth, double.PositiveInfinity));
        var scale = RootGrid.XamlRoot.RasterizationScale;
        // ResizeClient は、タイトルバーを自分で描いていても、タイトルバーの高さを上に足した大きさにする。中身だけの高さにするため、その分を引く
        var height = RootGrid.DesiredSize.Height - AppWindow.TitleBar.Height / scale;
        this.ResizeClientDip(WindowWidth, height, scale, roundUp: true);
        this.MoveCentered(DisplayArea.Primary.WorkArea);
    }

    /// <summary>保存して決定したら、画面を隠して、待っている側へ知らせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="saved">保存したか</param>
    private void OnCloseRequested(object? sender, bool saved)
    {
        _result = saved;
        AppWindow.Hide();
        _closed.TrySetResult(saved);
    }

    /// <summary>閉じたら結果を返す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Connection.PropertyChanged -= OnViewModelPropertyChanged;
        _closed.TrySetResult(_result);
    }
}
