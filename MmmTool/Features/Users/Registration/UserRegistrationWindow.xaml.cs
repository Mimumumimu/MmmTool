using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Utilities;
using MmmTool.Users.Core;
using Windows.Foundation;

namespace MmmTool.Features.Users.Registration;

/// <summary>ユーザーの登録ウィンドウ</summary>
/// <remarks>
/// 親を持たない、独立したウィンドウ (起動のあと、メインウィンドウがまだ出ていないときに出すため)。主モニターの中央に出す。
/// 幅は固定で、高さは中身に合わせて決めたら固定 (サイズ変更・最大化・最小化はできない)。× で閉じたときは「あとで」と同じ。
/// 開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// </remarks>
public sealed partial class UserRegistrationWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 440;

    /// <summary>閉じたときに結果を返す</summary>
    private readonly TaskCompletionSource<AppUser?> _closed = new();

    /// <summary>登録したユーザー。「あとで」・× なら null</summary>
    private AppUser? _result;

    /// <summary>ウィンドウの ViewModel</summary>
    public UserRegistrationViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public UserRegistrationWindow(UserRegistrationViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        ViewModel.CloseRequested += OnCloseRequested;
        RootGrid.Loaded += OnRootLoaded;
        Closed += OnClosed;
    }

    /// <summary>主モニターの中央に表示し、閉じるまで待つ</summary>
    /// <returns>登録したユーザー。「あとで」・× なら null</returns>
    public Task<AppUser?> ShowAsync()
    {
        this.UseFixedPresenter(isDialog: true, isResizable: false);

        // 高さは中身を読み込んでから決め直す (OnRootLoaded)。ここでは仮の大きさで中央に置く
        this.ResizeClientDip(WindowWidth, 360, this.GetDpiScale());
        this.MoveCentered(DisplayArea.Primary.WorkArea);

        Activate();
        this.BringToFront();
        return _closed.Task;
    }

    /// <summary>中身に合わせて高さを決め、中央に置き直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Measure(new Size(WindowWidth, double.PositiveInfinity));
        var scale = RootGrid.XamlRoot.RasterizationScale;
        // ResizeClient は、タイトルバーを自分で描いていても、タイトルバーの高さを上に足した大きさにする。中身だけの高さにするため、その分を引く
        var height = RootGrid.DesiredSize.Height - AppWindow.TitleBar.Height / scale;
        this.ResizeClientDip(WindowWidth, height, scale, roundUp: true);
        this.MoveCentered(DisplayArea.Primary.WorkArea);

        // 表示名を、そのまま打ち直せるように選択しておく
        NameBox.Focus(FocusState.Programmatic);
        NameBox.SelectAll();
    }

    /// <summary>表示名にフォーカスが来たら IME をオンにする (日本語の入力が多いため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnNameBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>登録・「あとで」で閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="registered">登録したユーザー。「あとで」なら null</param>
    private void OnCloseRequested(object? sender, AppUser? registered)
    {
        _result = registered;
        Close();
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
