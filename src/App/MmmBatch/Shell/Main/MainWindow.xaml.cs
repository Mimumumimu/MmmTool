using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MmmBatch.Sending;
using MmmSdk.Core.Components.WindowPositions;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;

namespace MmmBatch.Shell.Main;

/// <summary>メインウィンドウ (送信の状況を表示する)</summary>
/// <remarks>× ボタン・Alt+F4 では終了せず、トレイへ退避する (非表示にするだけで、アプリは動き続ける)。終了はトレイの「終了」から。</remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>既定の幅 (DIP)</summary>
    private const double DefaultWidth = 900;

    /// <summary>既定の高さ (DIP)</summary>
    private const double DefaultHeight = 480;

    /// <summary>アプリを終了中か</summary>
    /// <remarks>閉じる要求が「本当の終了」か「トレイへの退避」かを見分ける。</remarks>
    private bool _isExiting;

    /// <summary>ウィンドウを作る</summary>
    /// <param name="positions">ウィンドウの位置と大きさの保存・復元</param>
    /// <param name="content">送信の状況の画面 (今日の送信予定と履歴)</param>
    public MainWindow(IWindowPositionService positions, SendMainControl content)
    {
        InitializeComponent();
        ContentHost.Child = content;

        this.UseCustomTitleBar(AppTitleBar, AppIcon.FilePath);
        // 前回の位置と大きさを復元する (無い・画面外のときは、既定の大きさ。DPI に合わせる)。変わったら保存する。ウィンドウが閉じたら自分で後始末する
        WindowBoundsKeeper.Attach(this, positions, "MainWindow", DefaultWidth, DefaultHeight);
        AppWindow.Closing += OnClosing;
    }

    /// <summary>以降の閉じる要求で本当に閉じるようにする</summary>
    /// <remarks>トレイの「終了」から呼ぶ。</remarks>
    public void PrepareExit() => _isExiting = true;

    /// <summary>× ボタン・Alt+F4 では終了せず、トレイへ退避する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じる要求の情報</param>
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting)
        {
            return;
        }

        args.Cancel = true;
        sender.Hide();
    }
}
