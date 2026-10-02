using System.Drawing;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmTool.Core.Services;
using MmmTool.Interop;
using MmmTool.ViewModels;
using Windows.Graphics;

namespace MmmTool.Views;

/// <summary>デスクトップ通知のウィンドウ（見た目・表示内容と、ドラッグ・クリックで閉じる・位置の保存などの挙動）</summary>
/// <remarks>
/// 常に最前面で、フォーカスを奪わない（WS_EX_NOACTIVATE + 非アクティブ表示）。ウィンドウ全体のどこを掴んでもドラッグでき、
/// 動かさずに離したら（移動量がシステムのドラッグしきい値未満）クリックとみなして閉じる。
/// </remarks>
public sealed partial class NotificationWindow : Window
{
    /// <summary>ウィンドウの幅（DIP）</summary>
    private const double WindowWidth = 400;
    /// <summary>ウィンドウの最低の高さ（DIP）</summary>
    private const double MinWindowHeight = 160;
    /// <summary>ウィンドウの最大の高さ（DIP）。これを超える分は本文をスクロールする</summary>
    private const double MaxWindowHeight = 480;
    /// <summary>作業領域の端からの余白（既定位置。物理ピクセル）</summary>
    private const int ScreenMargin = 16;
    /// <summary>保存位置を「十分に見える」とみなす、ウィンドウ面積に対する見えている割合</summary>
    private const double VisibleRatio = 0.5;

    /// <summary>位置の保存・復元</summary>
    private readonly WindowPositionService _positions;

    /// <summary>ウィンドウのハンドル</summary>
    private readonly nint _hwnd;

    /// <summary>位置を保存・復元しているキー。まだ表示していなければ null</summary>
    private string? _positionKey;

    /// <summary>本文をクリックして閉じたときに呼ぶ処理</summary>
    private Action? _onClicked;

    /// <summary>左ボタンを押している最中か</summary>
    private bool _pressed;

    /// <summary>押下してからしきい値を超えて動かした（ドラッグ中）か</summary>
    private bool _dragging;

    /// <summary>この押下の間にリンクがクリックされたか</summary>
    private bool _linkClicked;

    /// <summary>押下したときのカーソル位置（画面座標）</summary>
    private NativeMethods.POINT _startCursor;

    /// <summary>押下したときのウィンドウ位置</summary>
    private PointInt32 _startWindow;

    /// <summary>ウィンドウの ViewModel</summary>
    public NotificationDialogViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    public NotificationWindow(NotificationDialogViewModel viewModel, WindowPositionService positions)
    {
        ViewModel = viewModel;
        _positions = positions;
        InitializeComponent();

        _hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);

        // コンテンツをタイトルバー領域まで広げる。標準のドラッグ領域（SetTitleBar）は使わず、ポインタイベントで自前実装する
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }
        NativeMethods.SetNoActivate(_hwnd);

        AppWindow.Closing += (_, _) => SavePosition();

        // リンクなど子が処理済みにした押下・移動・離しも受ける
        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        RootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        RootGrid.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        RootGrid.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnPointerCaptureLost), true);
    }

    /// <summary>内容を差し替えて表示し、到達を明滅で知らせる</summary>
    /// <remarks>
    /// 大きさ・位置を決めてから、フォーカスを奪わずに最前面へ表示する。
    /// 非アクティブで表示するとバインドの初回評価が走らないことがあるため、明示的に更新する。
    /// </remarks>
    public void Present(string title, IReadOnlyList<NotificationItem> items, Action? onClicked, string positionKey)
    {
        _onClicked = onClicked;
        ViewModel.Title = title;
        ViewModel.Items = items;
        Bindings.Update();
        BuildBody(items);

        FitHeight();
        var position = AppWindow.Position;
        if (_positionKey != positionKey)
        {
            _positionKey = positionKey;
            position = RestoreOrDefaultPosition(positionKey);
        }
        // 差し替えで高さが変わると、下や右がはみ出すことがあるため、毎回収める
        AppWindow.Move(KeepInWorkArea(position));

        NativeMethods.ShowTopmostNoActivate(_hwnd);

        BlinkStoryboard.Stop();
        BlinkStoryboard.Begin();
    }

    /// <summary>本文の Inlines を組み立てる</summary>
    /// <remarks>
    /// 項目を改行で区切って並べる。リンクはクリックで開く。
    /// リンクだけの行は当たり判定が行末の余白まで横に伸びてしまうため、リンクの直後に全角スペースの Run を 1 つ置いて文字幅側に留める。
    /// </remarks>
    private void BuildBody(IReadOnlyList<NotificationItem> items)
    {
        BodyText.Inlines.Clear();

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) BodyText.Inlines.Add(new LineBreak());

            var item = items[i];
            if (string.IsNullOrWhiteSpace(item.LinkPath))
            {
                BodyText.Inlines.Add(new Run { Text = item.Text });
                continue;
            }

            var path = item.LinkPath;
            var link = new Hyperlink();
            link.Inlines.Add(new Run { Text = item.Text });
            link.Click += (_, _) =>
            {
                // クリックで閉じる判定（OnPointerReleased）から、リンクのクリックを区別する
                _linkClicked = true;
                ViewModel.OpenLinkCommand.Execute(path);
            };
            BodyText.Inlines.Add(link);
            BodyText.Inlines.Add(new Run { Text = "　" });
        }
    }

    /// <summary>ウィンドウの DPI 倍率（論理サイズ→物理ピクセル）</summary>
    private double Scale => NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

    /// <summary>本文に合わせて高さを決める（表示前に確定する）</summary>
    /// <remarks>幅は固定。タイトル帯 + 本文の必要高さを実測し、最低・最大の高さの範囲で合わせる。</remarks>
    private void FitHeight()
    {
        var scale = Scale;

        RootGrid.Measure(new Windows.Foundation.Size(WindowWidth, double.PositiveInfinity));
        var height = Math.Clamp(RootGrid.DesiredSize.Height, MinWindowHeight, MaxWindowHeight);

        AppWindow.ResizeClient(new SizeInt32((int)Math.Round(WindowWidth * scale), (int)Math.Round(height * scale)));
    }

    /// <summary>保存位置が十分に見えていれば復元し、見えなければ既定位置（プライマリ作業領域の右下）を返す</summary>
    /// <remarks>現在のモニタ構成で判定する。</remarks>
    private PointInt32 RestoreOrDefaultPosition(string key)
    {
        var size = AppWindow.Size;

        if (_positions.Load(key) is { } saved)
        {
            // FindAll の返り値は foreach で列挙すると InvalidCastException になることがあるので、Count とインデクサで回す
            var displays = DisplayArea.FindAll();
            var workAreas = new List<Rectangle>(displays.Count);
            for (var i = 0; i < displays.Count; i++)
            {
                var work = displays[i].WorkArea;
                workAreas.Add(new Rectangle(work.X, work.Y, work.Width, work.Height));
            }

            if (WindowPositionService.IsVisibleEnough(new Rectangle(saved.X, saved.Y, size.Width, size.Height), workAreas, VisibleRatio))
            {
                return new PointInt32(saved.X, saved.Y);
            }
        }

        var area = DisplayArea.Primary.WorkArea;
        return new PointInt32(
            area.X + area.Width - size.Width - ScreenMargin,
            area.Y + area.Height - size.Height - ScreenMargin);
    }

    /// <summary>ウィンドウ全体が作業領域に収まるよう、位置をずらす</summary>
    /// <remarks>
    /// その位置にいちばん近いモニタの作業領域を使い、上下左右のはみ出しを内側へ寄せる。
    /// 作業領域より大きいときは左上に合わせる。保存する位置は変えない（ユーザーが置いた位置を残す）。
    /// </remarks>
    private PointInt32 KeepInWorkArea(PointInt32 position)
    {
        var size = AppWindow.Size;
        var work = DisplayArea.GetFromRect(new RectInt32(position.X, position.Y, size.Width, size.Height), DisplayAreaFallback.Nearest).WorkArea;

        var x = Math.Min(position.X, work.X + work.Width - size.Width);
        var y = Math.Min(position.Y, work.Y + work.Height - size.Height);
        return new PointInt32(Math.Max(x, work.X), Math.Max(y, work.Y));
    }

    /// <summary>現在の位置を保存する</summary>
    /// <remarks>閉じ方（クリック・×・Alt+F4）に関わらず閉じるときと、ドラッグが終わったときに呼ぶ。保存の失敗は握りつぶさない。</remarks>
    private async void SavePosition()
    {
        if (_positionKey is not { } key) return;
        var position = AppWindow.Position;
        await _positions.SaveAsync(key, new WindowPosition(position.X, position.Y));
    }

    #region ドラッグ移動・クリックで閉じる

    /// <summary>押下を記録する</summary>
    /// <remarks>右上のキャプションボタン領域・スクロールバーでの押下は対象外（閉じるのは OS のボタンに任せる）。</remarks>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootGrid);
        if (!point.Properties.IsLeftButtonPressed) return;
        if (IsOnCaptionButtons(point.Position) || IsOnScrollBar(e.OriginalSource as DependencyObject)) return;

        NativeMethods.GetCursorPos(out _startCursor);
        _startWindow = AppWindow.Position;
        _pressed = true;
        _dragging = false;
        _linkClicked = false;
    }

    /// <summary>しきい値を超えて動いたらドラッグとして、ウィンドウを動かす</summary>
    /// <remarks>
    /// ウィンドウが動くとポインタの相対座標が変わるため、画面座標のカーソル位置で移動量を測る。
    /// ドラッグになった時点でポインタをキャプチャする（リンクの押下を横取りしないよう、動かすまではしない）。
    /// </remarks>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed) return;

        NativeMethods.GetCursorPos(out var cursor);
        var dx = cursor.x - _startCursor.x;
        var dy = cursor.y - _startCursor.y;

        if (!_dragging)
        {
            if (Math.Abs(dx) < NativeMethods.GetSystemMetrics(NativeMethods.SM_CXDRAG)
                && Math.Abs(dy) < NativeMethods.GetSystemMetrics(NativeMethods.SM_CYDRAG))
            {
                return;
            }
            _dragging = true;
            RootGrid.CapturePointer(e.Pointer);
        }

        AppWindow.Move(new PointInt32(_startWindow.X + dx, _startWindow.Y + dy));
    }

    /// <summary>離したとき、ドラッグなら位置を保存し、動かしていなければクリックとして閉じる</summary>
    /// <remarks>
    /// リンクのクリックとの発火順に依存しないよう、閉じる判定は 1 サイクル遅らせ、その時点でリンクがクリックされていなければ閉じる。
    /// </remarks>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;

        if (_dragging)
        {
            RootGrid.ReleasePointerCapture(e.Pointer);
            EndDrag();
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_linkClicked) return;
            var onClicked = _onClicked;
            // Window.Close() では AppWindow.Closing が発火しないため、ここで保存する
            SavePosition();
            Close();
            onClicked?.Invoke();
        });
    }

    /// <summary>キャプチャを失ったら、押下の追跡を終える</summary>
    /// <remarks>離した通知（Released）より先にキャプチャ喪失が来ることがあるため、ドラッグ中だったらここでも終了処理（位置の保存）を行う。</remarks>
    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _pressed = false;
        EndDrag();
    }

    /// <summary>ドラッグを終えて、位置を保存する</summary>
    /// <remarks>ドラッグ中でなければ何もしない（離した通知とキャプチャ喪失の両方から呼ばれても 1 回だけ保存する）。</remarks>
    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        SavePosition();
    }

    /// <summary>右上のキャプションボタン（×など）の領域か</summary>
    /// <param name="position">ルート要素内の座標（DIP）。</param>
    private bool IsOnCaptionButtons(Windows.Foundation.Point position)
    {
        var scale = Scale;
        var titleBar = AppWindow.TitleBar;
        return position.Y * scale < titleBar.Height
            && position.X * scale >= WindowWidth * scale - titleBar.RightInset;
    }

    /// <summary>押下した要素がスクロールバーの中か</summary>
    private static bool IsOnScrollBar(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ScrollBar) return true;
        }
        return false;
    }

    #endregion
}
