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
    /// <param name="settings">× ボタンを押したときの動きの設定</param>
    /// <param name="exit">アプリの終了を頼む口</param>
    public MainWindow(NavigationHost navigation, IWindowPositionService positions, MainWindowSettingsService settings, AppExitService exit)
    {
        _settings = settings;
        _exit = exit;
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

    /// <summary>× ボタンを押したときの動きの設定</summary>
    private readonly MainWindowSettingsService _settings;

    /// <summary>アプリの終了を頼む口</summary>
    private readonly AppExitService _exit;

    /// <summary>× ボタンから、終了を頼み済みか</summary>
    private bool _exitRequested;

    /// <summary>×ボタン・Alt+F4 で、設定に従ってトレイへ退避するか終了する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じる要求の情報</param>
    /// <remarks>
    /// トレイへの退避は、非表示にするだけで、アプリは動き続ける。
    /// 終了は、ここでは閉じず、トレイの「終了」と同じ処理 (<see cref="AppExitService"/>)に任せる。各機能の後始末より先にウィンドウが閉じないようにするため。
    /// </remarks>
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting) return;
        args.Cancel = true;
        if (_settings.CloseButtonAction == CloseButtonAction.Exit)
        {
            // 終了を頼み済みなら、後始末の途中で閉じないよう、取り消すだけにする (連打で二重に頼まない)
            if (!_exitRequested)
            {
                _exitRequested = true;
                _exit.RequestExit();
            }
            return;
        }
        sender.Hide();
    }

    /// <summary>以降の閉じる要求で本当に閉じるようにする</summary>
    /// <remarks>トレイの「終了」から呼ぶ。</remarks>
    public void PrepareExit() => _isExiting = true;

    #endregion
}
