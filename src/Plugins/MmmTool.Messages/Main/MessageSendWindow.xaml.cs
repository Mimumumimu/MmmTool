using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.WindowPositions;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using Windows.Graphics;

namespace MmmTool.Messages.Main;

/// <summary>メッセージ送信のウィンドウ (トレイメニューから開く)</summary>
/// <remarks>
/// 普通のウィンドウ (モーダルではない)。アプリ内で 1 枚だけで、開いていれば前面に出す (<see cref="MessageWindowService"/>)。
/// 閉じたら破棄する。位置と大きさは SDK の <see cref="WindowBoundsKeeper"/> が保存し、次に開くとき復元する。
/// </remarks>
public sealed partial class MessageSendWindow : Window
{
    /// <summary>最初の幅 (DIP)</summary>
    private const double InitialWidth = 420;
    /// <summary>最初の高さ (DIP)</summary>
    private const double InitialHeight = 560;
    /// <summary>最小の幅 (DIP)</summary>
    private const double MinimumWidth = 360;
    /// <summary>最小の高さ (DIP)</summary>
    private const double MinimumHeight = 520;

    /// <summary>ウィンドウの位置と大きさの保存・復元</summary>
    private readonly IWindowPositionService _positions;

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">入力欄の ViewModel</param>
    /// <param name="positions">ウィンドウの位置と大きさの保存・復元</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public MessageSendWindow(MessageSendViewModel viewModel, IWindowPositionService positions, AppEnvironment environment)
    {
        _positions = positions;
        InitializeComponent();
        SendControl.ViewModel = viewModel;

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
    }

    /// <summary>前回の位置と大きさで表示する</summary>
    public void ShowWindow()
    {
        WindowBoundsKeeper.Attach(this, _positions, "MessageSendWindow", InitialWidth, InitialHeight, DefaultWindowPlacement.PrimaryBottomRight);
        var scale = this.GetDpiScale();

        this.UseFixedPresenter(isDialog: false, isResizable: true, new SizeInt32((int)(MinimumWidth * scale), (int)(MinimumHeight * scale)));
        Activate();
    }
}
