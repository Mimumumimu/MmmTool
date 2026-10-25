using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.WindowPositions;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;

namespace MmmToolAdmin.Shell.Main;

/// <summary>メインウィンドウ (左にサイドバー、右に機能ごとのページを表示する)</summary>
/// <remarks>
/// サイドバーとページの表示は SDK の <see cref="NavigationHost"/> が行う。このウィンドウは、タイトルバーと位置の保存を持つ。
/// 常駐しないので、閉じるとアプリが終了する。トレイへの退避は持たない。
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>既定の幅 (DIP)</summary>
    private const double DefaultWidth = 1100;

    /// <summary>既定の高さ (DIP)</summary>
    private const double DefaultHeight = 640;

    /// <summary>ウィンドウを作る</summary>
    /// <param name="positions">ウィンドウの位置と大きさの保存・復元</param>
    /// <param name="navigation">サイドバーと、選んだ項目のページ</param>
    public MainWindow(IWindowPositionService positions, NavigationHost navigation)
    {
        InitializeComponent();
        ContentHost.Child = navigation;
        HistoryButtons.Bind(navigation.ViewModel);

        this.UseCustomTitleBar(AppTitleBar, AppIcon.FilePath);
        this.KeepClickableInTitleBar(HistoryButtons);
        // 前回の位置と大きさを復元し、変わったら保存する。無いときや画面外のときは、DPI に合わせた既定の大きさにする。ウィンドウが閉じたら自分で後始末する
        WindowBoundsKeeper.Attach(this, positions, "MainWindow", DefaultWidth, DefaultHeight);
    }
}
