using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.ChannelEdit;

/// <summary>送信先の登録ウィンドウ (1 件の新規登録・編集)</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。幅は固定で、高さも固定 (サイズ変更・最大化・最小化はできない)。
/// タイトル帯をドラッグして移動できる。× で閉じたときはキャンセルと同じ。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// </remarks>
public sealed partial class NotificationChannelEditWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 480;

    /// <summary>ウィンドウの高さ (DIP。タイトルバーを含む)</summary>
    private const double WindowHeight = 480;

    /// <summary>タイトルバーの高さ (DIP)</summary>
    /// <remarks>ResizeClient は、タイトルバーを自分で描いていても、この分を上に足した大きさにする。</remarks>
    private const double TitleBarHeight = 32;

    /// <summary>閉じたときに結果を返す</summary>
    private readonly TaskCompletionSource<NotificationChannel?> _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>保存した内容。キャンセル・× なら null</summary>
    private NotificationChannel? _result;

    /// <summary>ウィンドウの ViewModel</summary>
    public NotificationChannelEditViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public NotificationChannelEditWindow(NotificationChannelEditViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);

        ViewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <param name="target">編集する送信先。新規なら null</param>
    /// <returns>保存した内容。キャンセル・× なら null</returns>
    public async Task<NotificationChannel?> ShowModalAsync(Window owner, NotificationChannel? target)
    {
        ViewModel.Load(target);

        _modal.SetOwner(owner);
        this.UseFixedPresenter(isDialog: true, isResizable: false);

        this.ResizeClientDip(WindowWidth, WindowHeight - TitleBarHeight, _modal.OwnerScale, roundUp: true);
        _modal.CenterOnOwner();

        _modal.Show();
        return await _closed.Task;
    }

    /// <summary>登録名にフォーカスが来たら IME をオンにする (日本語の入力が多いため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnNameBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>トピック名にフォーカスが来たら IME をオフにする (半角の英数字を打つため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnTopicBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOff();

    /// <summary>保存・キャンセルで閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="saved">保存した内容。キャンセルなら null</param>
    private void OnCloseRequested(object? sender, NotificationChannel? saved)
    {
        _result = saved;
        _modal.Close();
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
