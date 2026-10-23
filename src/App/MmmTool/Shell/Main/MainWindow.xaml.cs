using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.WindowPositions;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.Shell.Main;

/// <summary>メインウィンドウ (左にナビゲーション、右にページを表示する)</summary>
/// <remarks>サイドバーとページの表示は SDK の <see cref="NavigationHost"/> が行う。このウィンドウは、タイトルバー・位置の保存・トレイへの退避を持つ。</remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>既定の幅 (DIP)</summary>
    private const double DefaultWidth = 1280;

    /// <summary>既定の高さ (DIP)</summary>
    private const double DefaultHeight = 720;

    /// <summary>ウィンドウを作る</summary>
    /// <param name="navigation">サイドバーと、選んだ項目のページ</param>
    /// <param name="positions">ウィンドウの位置と大きさの保存・復元</param>
    public MainWindow(NavigationHost navigation, IWindowPositionService positions)
    {
        InitializeComponent();
        ContentHost.Child = navigation;
        HistoryButtons.Bind(navigation.ViewModel);

        this.UseCustomTitleBar(AppTitleBar, AppIcon.FilePath);
        this.KeepClickableInTitleBar(HistoryButtons);
        // 前回の位置と大きさを復元する (無い・画面外のときは、既定の 1280×720 DIP。DPI に合わせる)。変わったら保存する。ウィンドウが閉じたら自分で後始末する
        WindowBoundsKeeper.Attach(this, positions, "MainWindow", DefaultWidth, DefaultHeight);
        AppWindow.Closing += OnClosing;
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
}
