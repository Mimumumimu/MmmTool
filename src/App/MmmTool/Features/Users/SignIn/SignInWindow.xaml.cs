using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Utilities;
using MmmTool.Users.Core;
using Windows.Foundation;
using Windows.System;

namespace MmmTool.Features.Users.SignIn;

/// <summary>ログインウィンドウ</summary>
/// <remarks>
/// 親を持たない、独立したウィンドウ (起動のあと、メインウィンドウがまだ出ていないときに出すため)。主モニターの中央に出す。
/// 幅は固定で、高さは中身 (状態ごとに違う)に合わせて決め直す (サイズ変更・最大化・最小化はできない)。ログインは必須で、× で閉じたときは、ログインしないままなので、呼び出し側がアプリを終了する。
/// 開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// </remarks>
public sealed partial class SignInWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 440;

    /// <summary>閉じたときに結果を返す</summary>
    private readonly TaskCompletionSource<AppUser?> _closed = new();

    /// <summary>ログインしたユーザー。× なら null</summary>
    private AppUser? _result;

    /// <summary>ウィンドウの ViewModel</summary>
    public SignInViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public SignInWindow(SignInViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        ViewModel.CloseRequested += OnCloseRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        RootGrid.Loaded += OnRootLoaded;
        Closed += OnClosed;
    }

    /// <summary>新規登録のとき (と新しいパスワードのとき)だけ、確認入力を出す</summary>
    /// <param name="mode">画面の状態</param>
    /// <returns>確認入力を出すなら <see cref="Visibility.Visible"/></returns>
    public static Visibility IsConfirmVisible(SignInMode mode) => mode == SignInMode.SignIn ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>主モニターの中央に表示し、閉じるまで待つ</summary>
    /// <returns>ログインしたユーザー。× で閉じたら null</returns>
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
        FitHeight();
        FocusFirstInput();
    }

    /// <summary>状態が変わったら、高さを合わせ直して、入力欄にフォーカスを移す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SignInViewModel.Mode))
        {
            return;
        }

        // 表示の切り替え (x:Bind の更新)が済んでから測る
        DispatcherQueue.TryEnqueue(() =>
        {
            RootGrid.UpdateLayout();
            FitHeight();
            FocusFirstInput();
        });
    }

    /// <summary>今の中身の高さに、ウィンドウの高さを合わせて、中央に置き直す</summary>
    private void FitHeight()
    {
        RootGrid.Measure(new Size(WindowWidth, double.PositiveInfinity));
        var scale = RootGrid.XamlRoot.RasterizationScale;
        // ResizeClient は、タイトルバーを自分で描いていても、タイトルバーの高さを上に足した大きさにする。中身だけの高さにするため、その分を引く
        var height = RootGrid.DesiredSize.Height - AppWindow.TitleBar.Height / scale;
        this.ResizeClientDip(WindowWidth, height, scale, roundUp: true);
        this.MoveCentered(DisplayArea.Primary.WorkArea);
    }

    /// <summary>状態に合った、最初の入力欄にフォーカスを移す</summary>
    private void FocusFirstInput()
    {
        if (ViewModel.IsLoginNameEditable)
        {
            LoginNameBox.Focus(FocusState.Programmatic);
            LoginNameBox.SelectAll();
        }
        else
        {
            PasswordBox.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>表示名にフォーカスが来たら IME をオンにする (日本語の入力が多いため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnDisplayNameBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>Enter キーで、主な操作を行う (ボタンにフォーカスがあるときは、ボタンの操作に任せる)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キーの情報</param>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && e.OriginalSource is not Microsoft.UI.Xaml.Controls.Button && ViewModel.SubmitCommand.CanExecute(null))
        {
            e.Handled = true;
            ViewModel.SubmitCommand.Execute(null);
        }
    }

    /// <summary>ログインして閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="user">ログインしたユーザー</param>
    private void OnCloseRequested(object? sender, AppUser? user)
    {
        _result = user;
        Close();
    }

    /// <summary>閉じたら結果を返す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _closed.TrySetResult(_result);
    }
}
