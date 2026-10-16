using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Controls;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Schedule;

/// <summary>スケジュールのページ (作業の予定と実績を、日付を横に並べて見る。サイドバーでは作業管理の子)</summary>
/// <remarks>
/// 表 (<see cref="TreeGridView"/>)の左に、名前・担当・進捗を固定して置き、右の 1 つの広い列に、日付の棒を行ごとに描く (<see cref="ScheduleTimelineCell"/>)。
/// 日付の軸が変わったとき (範囲・単位)は、広い列の幅と見出しを作り直す。単位を変えたときは、見ている範囲の中央の日付を保つ。
/// </remarks>
public sealed partial class SchedulePage : Page
{
    /// <summary>今日を開いたとき・「今日」を押したときに、今日を置く位置 (見える範囲の左端からの割合)</summary>
    private const double TodayRatio = 0.25;

    /// <summary>日付の棒を出す、広い列</summary>
    private readonly TreeGridColumn _timelineColumn;

    /// <summary>単位を変えたときに、見ている範囲の中央に保つ日付 (軸が作り直されたら、その日付へ動かす)</summary>
    private DateOnly? _anchor;

    /// <summary>保つ日付を置く位置 (見える範囲の左端からの割合。今日を基準にするときは <see cref="TodayRatio"/>、見ている範囲の中央を保つときは 0.5)</summary>
    private double _anchorRatio = 0.5;

    /// <summary>マウスカーソルを処理待ちの形にする口</summary>
    private readonly IBusyCursor _busyCursor;

    /// <summary>読み込み中の、処理待ちの入れ物 (読み込み中でなければ null)</summary>
    private IDisposable? _busyScope;

    /// <summary>ページの ViewModel</summary>
    public ScheduleViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    /// <param name="busyCursor">マウスカーソルを処理待ちの形にする口</param>
    public SchedulePage(ScheduleViewModel viewModel, IBusyCursor busyCursor)
    {
        ViewModel = viewModel;
        _busyCursor = busyCursor;
        InitializeComponent();
        ProjectPickerControl.Attach(ViewModel.ProjectPicker);

        // 日付の棒の列は、表示だけで、フォーカスを受けない (幅が表より広く、フォーカスが来ると、左端までスクロールが動くため)。見出しは、軸ごとに作る
        _timelineColumn = new TreeGridColumn
        {
            Key = "Timeline",
            Header = "",
            Width = 100,
            IsFocusable = false,
            CellTemplate = (DataTemplate)Resources["TimelineCell"],
            HeaderFactory = BuildTimelineHeader,
        };
        TimelineGrid.Columns.Add(Column("Name", "名前", 260, "NameCell", tree: true));
        TimelineGrid.Columns.Add(Column("Assignee", "担当", 96, "AssigneeCell", visible: ViewModel.HasAssignees));
        TimelineGrid.Columns.Add(Column("Progress", "進捗", 96, "ProgressCell"));
        TimelineGrid.Columns.Add(_timelineColumn);
        TimelineGrid.HeaderHeight = ScheduleHeader.Height;
        TimelineGrid.RefreshColumns();
        TimelineGrid.ItemsSource = ViewModel.Rows;
        TimelineGrid.RowToggleRequested += (_, e) => ViewModel.Toggle((ScheduleRow)e.Row);

        ViewModel.AxisChanged += OnAxisChanged;

        // 案件 1 つぶんの行の塊を差し込む・抜いたあとは、表に行を付け直させる (見える範囲に、行が作り直されないことがあるため)
        ViewModel.RowsBlockChanged += (_, _) => TimelineGrid.RebindRows();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScheduleViewModel.IsLoading))
            {
                ApplyLoadingCursor();
            }
        };
        Unloaded += (_, _) =>
        {
            ViewModel.Suspend();
            _busyScope?.Dispose();
            _busyScope = null;
        };
        ActualThemeChanged += (_, _) => RefreshTimeline();
    }

    /// <summary>読み込み中は、ウィンドウ全体のマウスカーソルを砂時計にする (サイドバーを押した直後は、マウスがサイドバーの上にあるため。終わったら、元に戻す)</summary>
    private void ApplyLoadingCursor()
    {
        if (ViewModel.IsLoading)
        {
            _busyScope ??= _busyCursor.Begin();
        }
        else if (_busyScope is { } scope)
        {
            _busyScope = null;
            EndBusyAfterRenderAsync(scope).Forget();
        }
    }

    /// <summary>表の行・見出しが画面に描かれてから、処理待ちを終える</summary>
    /// <param name="scope">終える処理待ちの入れ物</param>
    /// <returns>終わりの完了を表すタスク</returns>
    /// <remarks>読み込みが終わった時点では、行と見出しはまだ作られていない (そのあとのレイアウトで作られる)。続けて動かす位置の指定 (<see cref="ScrollTo"/>)のあとに終えるため、2 回待つ。</remarks>
    private async Task EndBusyAfterRenderAsync(IDisposable scope)
    {
        await WaitForRenderAsync();
        await WaitForRenderAsync();
        scope.Dispose();
    }

    /// <summary>左の固定の列の定義を作る</summary>
    /// <param name="key">列のキー</param>
    /// <param name="header">見出し</param>
    /// <param name="width">幅</param>
    /// <param name="templateKey">セルの見た目のリソースのキー</param>
    /// <param name="tree">木構造の列か</param>
    /// <param name="visible">表示するか</param>
    /// <returns>列の定義 (固定の列)</returns>
    private TreeGridColumn Column(string key, string header, double width, string templateKey, bool tree = false, bool visible = true)
        => new()
        {
            Key = key,
            Header = header,
            Width = width,
            IsFrozen = true,
            IsTree = tree,
            IsVisible = visible,
            CellTemplate = (DataTemplate)Resources[templateKey],
        };

    /// <summary>日付の棒の列の見出しを作る</summary>
    /// <returns>見出しの部品 (軸がまだ無いときは、空)</returns>
    private UIElement BuildTimelineHeader()
        => ViewModel.Axis is { } axis ? ScheduleHeader.Build(axis, SchedulePalette.For(ActualTheme), ViewModel.Today) : new Grid();

    /// <summary>ページを開いたときに、保存先から読み直して、今日が見える位置へ動かす</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 砂時計は、画面に載った直後に出す (見出しの作り直しや、描画の待ちより先に)
        ViewModel.BeginLoading();

        // 単位は、開くたびに「日」から始める (ページは使い回されるので、前に選んだ単位を戻す。先に ViewModel を戻すので、選択の変更は何もしない)
        _anchor = null;
        ViewModel.UnitIndex = (int)ScheduleUnit.Day;
        UnitBar.SelectedItem = UnitBar.Items[(int)ScheduleUnit.Day];

        // ページは使い回されるので、見えない間にテーマが変わっていても、開いたときの見出しの色を、今のテーマに合わせる
        RefreshTimeline();

        // 画面を先に切り替えて描いてから、読み込みを始める (クリックした瞬間に固まらないため)
        await WaitForRenderAsync();
        await ViewModel.InitializeAsync();
        ScrollTo(ViewModel.Today, TodayRatio);
    }

    /// <summary>画面の切り替えが描かれるまで待つ</summary>
    /// <returns>待ちの完了を表すタスク</returns>
    /// <remarks>低い優先度で呼ばれるので、すでに待っている描画・レイアウトのあとに動く。</remarks>
    private Task WaitForRenderAsync()
    {
        var source = new TaskCompletionSource();
        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => source.SetResult()))
        {
            source.SetResult();
        }
        return source.Task;
    }

    /// <summary>「今日」が押されたら、今日が見える位置へ動かす</summary>
    /// <param name="sender">ボタン</param>
    /// <param name="e">イベントの情報</param>
    private void OnTodayClick(object sender, RoutedEventArgs e) => ScrollTo(ViewModel.Today, TodayRatio);

    /// <summary>単位が選ばれたら、見ている範囲の中央の日付を保って、軸を作り直す</summary>
    /// <param name="sender">単位の選択</param>
    /// <param name="args">選択の情報</param>
    private void OnUnitSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var index = sender.Items.IndexOf(sender.SelectedItem);
        if (index < 0 || index == ViewModel.UnitIndex)
        {
            return;
        }

        if (ViewModel.Axis is { } visual)
        {
            // 軸の全体が見えているとき (月の単位)は、「見ている範囲の中央」に意味が無い (軸の真ん中や、軸の外の日付になる)ので、今日を基準にする。
            // 途中を見ているときは、見ている範囲の中央の日付を保つ
            var visible = TimelineGrid.ViewportWidth - TimelineGrid.FrozenWidth;
            var allVisible = visual.Axis.Width <= visible;
            _anchor = allVisible
                ? ViewModel.Today
                : visual.Axis.DateAt(TimelineGrid.HorizontalOffset + (visible / 2));
            _anchorRatio = allVisible ? TodayRatio : 0.5;
        }
        ViewModel.UnitIndex = index;
    }

    /// <summary>日付の軸が変わったら、広い列の幅と見出しを作り直し、保つ日付があれば、その日付を決めた位置に動かす</summary>
    /// <param name="sender">ViewModel</param>
    /// <param name="e">イベントの情報</param>
    private void OnAxisChanged(object? sender, EventArgs e)
    {
        RefreshTimeline();
        if (_anchor is { } anchor)
        {
            _anchor = null;
            ScrollTo(anchor, _anchorRatio);
        }
    }

    /// <summary>広い列の幅を軸に合わせ、見出しとセルを作り直す</summary>
    private void RefreshTimeline()
    {
        if (ViewModel.Axis is not { } visual)
        {
            return;
        }

        _timelineColumn.Width = visual.Axis.Width;
        TimelineGrid.RefreshColumns();
    }

    /// <summary>日付が、見える範囲の決まった位置になるよう、横に動かす</summary>
    /// <param name="date">日付</param>
    /// <param name="ratio">見える範囲 (固定の列を除く)の左端からの割合 (0.5 で中央)</param>
    /// <remarks>列の幅を変えた直後は、幅が決まっていないので、レイアウトが済んでから動かす。</remarks>
    private void ScrollTo(DateOnly date, double ratio)
        => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (ViewModel.Axis is not { } visual)
            {
                return;
            }

            var visible = TimelineGrid.ViewportWidth - TimelineGrid.FrozenWidth;
            TimelineGrid.ScrollHorizontally(visual.Axis.X(date) + (visual.Axis.DayWidth / 2) - (visible * ratio));
        });
}
