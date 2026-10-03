using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MmmSdk.Core.WindowPositions;
using MmmSdk.WinUI.Windowing;

namespace MmmTool.Shell;

/// <summary>メインウィンドウ（左にナビゲーション、右にページを表示する）</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>既定の幅（DIP）</summary>
    private const double DefaultWidth = 1280;

    /// <summary>既定の高さ（DIP）</summary>
    private const double DefaultHeight = 720;

    /// <summary>項目に対応するページ</summary>
    private readonly PageProvider _pages;

    /// <summary>ウィンドウの ViewModel</summary>
    public MainViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="pages">項目に対応するページ</param>
    /// <param name="positions">ウィンドウの位置と大きさの保存・復元</param>
    public MainWindow(MainViewModel viewModel, PageProvider pages, IWindowPositionService positions)
    {
        ViewModel = viewModel;
        _pages = pages;

        InitializeComponent();

        this.UseCustomTitleBar(AppTitleBar, AppIcon.FilePath);
        // 前回の位置と大きさを復元する（無い・画面外のときは、既定の 1280×720 DIP。DPI に合わせる）。変わったら保存する。ウィンドウが閉じたら自分で後始末する
        WindowBoundsKeeper.Attach(this, positions, "MainWindow", DefaultWidth, DefaultHeight);
        AppWindow.Closing += OnClosing;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        NavigateTo(ViewModel.SelectedItem);
    }

    #region トレイへの退避・再表示

    /// <summary>アプリを終了中か</summary>
    /// <remarks>閉じる要求が「本当の終了」か「トレイへの退避」かを見分ける。</remarks>
    private bool _isExiting;

    /// <summary>×ボタン・Alt+F4 では終了せず、トレイへ退避する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じる要求の情報</param>
    /// <remarks>非表示にするだけで、アプリは動き続ける。</remarks>
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting) return;
        args.Cancel = true;
        sender.Hide();
    }

    /// <summary>以降の閉じる要求で本当に閉じるようにする</summary>
    /// <remarks>トレイの「終了」から呼ぶ。</remarks>
    public void PrepareExit() => _isExiting = true;

    #endregion

    /// <summary>選択が変わったら、そのページへ移動する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedItem))
        {
            NavigateTo(ViewModel.SelectedItem);
        }
    }

    /// <summary>項目に対応するページを表示する</summary>
    /// <param name="item">表示するナビゲーション項目</param>
    private void NavigateTo(NavigationItem? item)
    {
        if (item is null || _pages.GetPage(item) is not { } page)
        {
            return;
        }

        ContentFrame.Content = page;
    }
}
