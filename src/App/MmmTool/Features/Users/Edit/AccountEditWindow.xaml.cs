using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using Windows.System;

namespace MmmTool.Features.Users.Edit;

/// <summary>アカウントの項目 (表示名・ログイン名・パスワード)を編集するウィンドウ</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。幅は固定で、高さは中身 (項目ごとに違う)に合わせて決める。サイズ変更・最大化・最小化はできない。
/// 保存・キャンセル・× のどれでも閉じる。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// </remarks>
public sealed partial class AccountEditWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 440;

    /// <summary>閉じたことを知らせる</summary>
    private readonly TaskCompletionSource _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>ウィンドウの ViewModel</summary>
    public AccountEditViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public AccountEditWindow(AccountEditViewModel viewModel, AppEnvironment environment)
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
    /// <param name="kind">編集する項目</param>
    /// <returns>ウィンドウが閉じたことを表すタスク</returns>
    public async Task ShowModalAsync(Window owner, AccountEditKind kind)
    {
        ViewModel.Initialize(kind);
        Title = ViewModel.Heading;

        _modal.SetOwner(owner);
        this.UseFixedPresenter(isDialog: true, isResizable: false);

        // 高さは中身を読み込んでから決め直す (OnRootLoaded)。ここでは仮の大きさで親の中央に置く
        this.ResizeClientDip(WindowWidth, 300, _modal.OwnerScale);
        _modal.CenterOnOwner();

        _modal.Show();
        await _closed.Task;
    }

    /// <summary>中身に合わせて高さを決め、最初の入力欄にフォーカスを移す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (this.ResizeToContentHeight(RootGrid, WindowWidth))
        {
            _modal.CenterOnOwner();
        }

        if (ViewModel.ShowValue)
        {
            ValueBox.Focus(FocusState.Programmatic);
            ValueBox.SelectAll();
        }
        else
        {
            CurrentPasswordBox.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>表示名の入力欄にフォーカスが来たら IME をオンにする (日本語の入力が多いため。ログイン名は英数字が多いので、オンにしない)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnValueBoxGotFocus(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.NeedsCurrentPassword)
        {
            ImeControl.TurnOn();
        }
    }

    /// <summary>Enter キーで保存する (ボタンにフォーカスがあるときは、ボタンの操作に任せる)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キーの情報</param>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && e.OriginalSource is not Button && ViewModel.SaveCommand.CanExecute(null))
        {
            e.Handled = true;
            ViewModel.SaveCommand.Execute(null);
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
        _closed.TrySetResult();
    }
}
