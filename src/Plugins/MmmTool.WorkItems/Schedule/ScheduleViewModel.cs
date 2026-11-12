using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Controls;
using MmmTool.WorkItems.Core;
using MmmTool.WorkItems.ProjectPicker;

namespace MmmTool.WorkItems.Schedule;

/// <summary>スケジュールのページ</summary>
/// <remarks>
/// 作業の予定と実績を日付を横に並べて見る。
/// 見るだけ。
/// 案件・グループ・作業と、他の人の更新の取り込みは作業管理と同じ <see cref="WorkItemService"/> を使う。
/// 実績 (すべての日の記録)はページを開くたびに読み、離れたら捨てる。
/// 読むのは <see cref="InitializeAsync"/>、捨てるのは <see cref="Suspend"/>。
/// 「表示する案件」「自分の担当だけ」は作業管理と同じ設定を共有する。
/// </remarks>
public sealed partial class ScheduleViewModel : ObservableObject, IDisposable
{
    /// <summary>作業管理の読み書き</summary>
    private readonly WorkItemService _service;

    /// <summary>設定ストア</summary>
    private readonly ISettingsStore _settings;

    /// <summary>マウスカーソルを処理待ちの形にする口</summary>
    private readonly IBusyCursor _busyCursor;

    /// <summary>行の識別から表の行を引く</summary>
    /// <remarks>使い回す</remarks>
    private readonly Dictionary<WorkKey, ScheduleRow> _rowByKey = [];

    /// <summary>画面で選んだ状態</summary>
    /// <remarks>ページを作り直しても引き継ぐためにページの外で持つ</remarks>
    private readonly ScheduleViewState _state;

    /// <summary>利用者が閉じた案件・グループ</summary>
    /// <remarks>
    /// 状態クラスと同じ集合
    /// 既定ではすべて開く
    /// </remarks>
    private readonly HashSet<WorkKey> _collapsed;

    /// <summary>ページを開いている間か</summary>
    /// <remarks>離れている間はサービスの変更で表を作り直さない</remarks>
    private bool _active;

    /// <summary>続いている、開いたときの読み込みの数で、1 つ以上の間は途中のサービスの変更で、表を作り直さない</summary>
    /// <remarks>ページの出し入れで、重なることがある。</remarks>
    private int _loadCount;

    /// <summary>他の人の更新の取得をこのページが始めているか</summary>
    /// <remarks>二重に始めない・止め損なわない</remarks>
    private bool _polling;

    /// <summary>作っている最中か</summary>
    /// <remarks>設定の読み込みで、値の変更の処理を動かさないため。</remarks>
    private bool _initializing = true;

    /// <summary>案件マスターが空か</summary>
    /// <remarks>表示する案件が無いときの案内を決める</remarks>
    private bool _masterEmpty;

    /// <summary>表の行で、見えている行を 1 つにならしたもの</summary>
    /// <remarks>木の順</remarks>
    public ObservableCollection<ScheduleRow> Rows { get; } = [];

    /// <summary>総合計の行で、表示している案件を合わせた実績を出す</summary>
    /// <remarks>
    /// 表の上に固定する。
    /// 合わせるのは「表示する案件」で選んでいる案件。
    /// 案件の行の合計と同じく、「自分の担当だけ」と「完了」で隠した作業も含めて、全員の作業から計算する。
    /// </remarks>
    public ScheduleRow TotalRow { get; } = ScheduleRow.CreateTotal();

    /// <summary>総合計の値</summary>
    /// <remarks>案件の出し入れに合わせて、足し引きする。</remarks>
    private readonly ScheduleTotal _total = new();

    /// <summary>総合計に足してある案件ごとの実績の合計と実績の棒</summary>
    /// <remarks>案件を表示から外したとき、足したときと同じ値を、総合計から引くために持つ。</remarks>
    private readonly Dictionary<int, (double Hours, IReadOnlyList<ScheduleActualRun> Runs)> _projectTotals = [];

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>表示する案件を選ぶ画面の ViewModel</summary>
    /// <remarks>作業管理と同じ設定を使う</remarks>
    public ProjectPickerViewModel ProjectPicker { get; }

    /// <summary>担当者を扱うか</summary>
    /// <remarks>ローカルモードは false で、担当の列と絞り込みを出さない。</remarks>
    public bool HasAssignees => _service.HasAssignees;

    /// <summary>更新ボタンを出すか</summary>
    /// <remarks>他の人の更新がある DB モードだけで、ローカルモードは false。</remarks>
    public bool CanRefresh => _service.HasAssignees;

    /// <summary>自分が担当の作業だけを表示するか</summary>
    [ObservableProperty]
    public partial bool MineOnly { get; set; }

    /// <summary>表示の単位の番号</summary>
    /// <remarks>
    /// <see cref="ScheduleUnit"/> の値と同じ。
    /// 保存しない。
    /// 状態クラス (<see cref="ScheduleViewState"/>)が引き継ぎ、起動したときは日になる。
    /// </remarks>
    [ObservableProperty]
    public partial int UnitIndex { get; set; }

    /// <summary>「完了」の選択肢の番号</summary>
    /// <remarks>
    /// <see cref="CompletedDisplay"/> の値と同じ。
    /// 画面で選んだ値は保存しない。
    /// 状態クラス (<see cref="ScheduleViewState"/>)が引き継ぎ、起動したときの値は設定の起動時の表示 (<see cref="WorkItemDisplaySettings.ScheduleCompleted"/>)で決まる。
    /// 作業管理の「過去の完了」とは共有しない。
    /// </remarks>
    [ObservableProperty]
    public partial int CompletedIndex { get; set; }

    /// <summary>保存先から読み込み中か</summary>
    /// <remarks>
    /// マウスカーソルを砂時計にする。
    /// 読み込んでいる間も前に開いたときの表はそのまま出しておく。
    /// 空にして、ちらつかせないため。
    /// </remarks>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>行が 1 つも無いか</summary>
    /// <remarks>案内を出す</remarks>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>行が無いときの案内</summary>
    /// <remarks>表示する案件を選ぶか、案件を追加するか。</remarks>
    [ObservableProperty]
    public partial string EmptyMessage { get; private set; } = "";

    /// <summary>今日の日付</summary>
    public DateOnly Today => _service.Today;

    /// <summary>今の日付の軸で、範囲と単位</summary>
    /// <remarks>まだ作っていなければ null</remarks>
    public ScheduleAxisVisual? Axis { get; private set; }

    /// <summary>日付の軸が変わった</summary>
    /// <remarks>範囲・単位が変わったときで、列の幅と見出しを作り直すため。</remarks>
    public event EventHandler? AxisChanged;

    /// <summary>行の塊を差し込んだ・抜いた</summary>
    /// <remarks>表に行の一覧を付け直させるため</remarks>
    public event EventHandler? RowsBlockChanged;

    /// <summary>行の一覧を作り直した</summary>
    /// <remarks>値だけが変わったときも含み、列の幅を直すきっかけにする。</remarks>
    public event EventHandler? Rebuilt;

    /// <summary>ViewModel を作る</summary>
    /// <param name="service">作業管理の読み書き</param>
    /// <param name="settings">設定ストア</param>
    /// <param name="projectPicker">表示する案件を選ぶ画面の ViewModel</param>
    /// <param name="busyCursor">マウスカーソルを処理待ちの形にする口</param>
    /// <param name="state">画面で選んだ状態 (ページを作り直しても引き継ぐ)</param>
    public ScheduleViewModel(WorkItemService service, ISettingsStore settings, ProjectPickerViewModel projectPicker, IBusyCursor busyCursor, ScheduleViewState state)
    {
        _service = service;
        _state = state;
        _collapsed = state.Collapsed;
        _settings = settings;
        _busyCursor = busyCursor;
        ProjectPicker = projectPicker;

        service.Changed += OnServiceChangedEvent;
        service.RefreshFailed += OnServiceRefreshFailed;
        service.MineOnlyChanged += OnServiceMineOnlyChanged;
        MineOnly = settings.Get(WorkItemSettingKeys.MineOnly, false);
        UnitIndex = (int)state.Unit;
        CompletedIndex = (int)state.Completed;
        _initializing = false;
    }

    /// <summary>読み込み中の表示を読み込みの開始より先に始める</summary>
    /// <remarks>
    /// ページが画面に載ってから、読み込みを始めるまでの間にも、待ち時間があるため。
    /// 終わりは <see cref="InitializeAsync"/> が決める。
    /// </remarks>
    public void BeginLoading() => IsLoading = true;

    /// <summary>他の人の更新を今すぐ取り込む</summary>
    /// <returns>取り込みの完了を表すタスク</returns>
    /// <remarks>失敗は定期的な取り込みと同じく、サービスの通知で画面に出す。</remarks>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error.Clear();
        await _service.RefreshNowAsync();
    }

    /// <summary>ページを開いたときに保存先から読み直して表を作り、他の人の更新の取得を始める</summary>
    /// <returns>初期化の完了を表すタスク</returns>
    /// <remarks>
    /// 作業管理の表の入力日をずらさないよう、記録を読んでいる日 (<see cref="WorkItemService.Day"/>)のまま読み直す。
    /// 読み込みの途中は表を作り直さない。
    /// 案件・実績を読むたびに作り直すと、実績が空の表を挟んで、3 回作ることになるため。
    /// 読み終えてから 1 回だけ作る。
    /// </remarks>
    public async Task InitializeAsync()
    {
        Error.Clear();
        IsLoading = true;

        // 「自分の担当だけ」は、作業管理でも切り替えられる。同じ設定を使う。画面を開くたびに、設定の値に合わせる
        var mineOnly = _settings.Get(WorkItemSettingKeys.MineOnly, false);
        if (mineOnly != MineOnly)
        {
            _initializing = true;
            MineOnly = mineOnly;
            _initializing = false;
        }

        _active = true;
        _loadCount++;
        try
        {
            await _service.LoadAsync(_service.Day);
            await _service.LoadScheduleRecordsAsync();
            _masterEmpty = (await _service.SearchProjectsAsync("", activeOnly: false, selectedOnly: false, selectedFirst: false, 0, 1)).Count == 0;
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        finally
        {
            _loadCount--;
        }

        // ページの出し入れで、読み込みが重なったときは、あとから始まった読み込みが続いている。表の作成と終わりの処理は、最後の読み込みに任せる
        // (先に終わったほうが作ると、あとの読み込みが捨てた実績のない表を作り、そのあと、もう 1 回作り直すことになるため)
        if (_loadCount > 0)
        {
            return;
        }

        Rebuild();
        IsLoading = false;

        // 読んでいる間に、ページを離れていたら、取得を始めない (離れたときの停止は、始める前に済んでいるため)
        if (_active && !_polling)
        {
            _polling = true;
            _service.StartPolling();
        }
    }

    /// <summary>ページを離れたときに他の人の更新の取得を止め、実績を捨てる</summary>
    public void Suspend()
    {
        _active = false;
        if (_polling)
        {
            _polling = false;
            _service.StopPolling();
        }
        _service.ReleaseScheduleRecords();
    }

    /// <summary>サービスの変更の通知を受け取る</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更の内容</param>
    private void OnServiceChangedEvent(object? sender, WorkChangedEventArgs e) => OnServiceChanged(e);

    /// <summary>取り込みに失敗したことを画面のエラーに出す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="message">失敗の内容</param>
    private void OnServiceRefreshFailed(object? sender, string message) => Error.Show(message);

    /// <summary>サービスの通知の購読を解除する</summary>
    /// <remarks>
    /// サービスはアプリの終了まで生きるので、購読したままだと、ページを捨てたあとも、この ViewModel とページがメモリに残り続ける。
    /// ページを開いたまま捨てられるとき (外したウィンドウを閉じる・機能をオフにする)は取得の参照と実績も返す。
    /// </remarks>
    public void Dispose()
    {
        _service.Changed -= OnServiceChangedEvent;
        _service.RefreshFailed -= OnServiceRefreshFailed;
        _service.MineOnlyChanged -= OnServiceMineOnlyChanged;
        if (_active)
        {
            Suspend();
        }
    }

    /// <summary>案件・グループを開く・閉じる</summary>
    /// <param name="row">押された行</param>
    public void Toggle(ScheduleRow row)
    {
        if (!_collapsed.Remove(row.Key))
        {
            _collapsed.Add(row.Key);
        }
        RebuildBusyAsync().Forget();
    }

    /// <summary>自分の担当だけを表示するかが変わったら、設定に残して、表を作り直す</summary>
    /// <param name="value">自分の担当だけにするか</param>
    /// <remarks>読み込みはやり直さない</remarks>
    partial void OnMineOnlyChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _settings.SetAsync(WorkItemSettingKeys.MineOnly, value).Forget();
        _service.NotifyMineOnlyChanged(value);
        RebuildBusyAsync().Forget();
    }

    /// <summary>作業管理で「自分の担当だけ」が変わったとき、この画面の値を合わせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="value">新しい値</param>
    /// <remarks>
    /// 設定へは変えた側が保存する。
    /// 画面が見えていなければ、次に開くときに、設定から読む。
    /// </remarks>
    private void OnServiceMineOnlyChanged(object? sender, bool value)
    {
        if (value == MineOnly)
        {
            return;
        }

        _initializing = true;
        MineOnly = value;
        _initializing = false;
        if (_active)
        {
            RebuildBusyAsync().Forget();
        }
    }

    /// <summary>表示の単位が変わったら軸を作り直す</summary>
    /// <param name="value">単位の番号</param>
    /// <remarks>全部の日付の棒を描き直すので、砂時計を出す。</remarks>
    partial void OnUnitIndexChanged(int value)
    {
        if (!_initializing)
        {
            if (Enum.IsDefined((ScheduleUnit)value))
            {
                _state.Unit = (ScheduleUnit)value;
            }
            RebuildBusyAsync().Forget();
        }
    }

    /// <summary>「完了」の選択が変わったら表を作り直す</summary>
    /// <param name="value">選択肢の番号</param>
    /// <remarks>
    /// 読み込みはやり直さない。
    /// 行の出し入れが多いので、砂時計を出す。
    /// </remarks>
    partial void OnCompletedIndexChanged(int value)
    {
        if (!_initializing)
        {
            if (Enum.IsDefined((CompletedDisplay)value))
            {
                _state.Completed = (CompletedDisplay)value;
            }
            RebuildBusyAsync().Forget();
        }
    }

    /// <summary>マウスカーソルを砂時計にして表を作り直す</summary>
    /// <returns>作り直しの完了を表すタスク</returns>
    /// <remarks>長く動くので、先に砂時計を描かせる。</remarks>
    private Task RebuildBusyAsync() => _busyCursor.RunAsync(Rebuild);

    /// <summary>サービスの内容から軸と行の一覧を作り直す</summary>
    /// <remarks>同じ行は使い回し、差分だけを一覧に反映する。</remarks>
    private void Rebuild()
    {
        if (!_active || _loadCount > 0)
        {
            return;
        }

        var snapshot = _service.BuildSchedule(MineOnly, Completed);
        var unit = Enum.IsDefined((ScheduleUnit)UnitIndex) ? (ScheduleUnit)UnitIndex : ScheduleUnit.Day;
        var axis = ScheduleAxis.Create(snapshot.AxisStart, snapshot.AxisEnd, unit);
        var axisChanged = Axis is null || Axis.Axis != axis;
        if (axisChanged)
        {
            Axis = new ScheduleAxisVisual(axis);
        }

        var target = new List<ScheduleRow>();
        var alive = new HashSet<WorkKey>();
        _projectTotals.Clear();
        _total.Clear();
        foreach (var root in snapshot.Roots)
        {
            Visit(root, 0, snapshot, target, alive);
            AddProjectTotal(root, snapshot);
        }
        UpdateTotalRow();

        TreeGridRowSync.Sync(Rows, target);
        foreach (var key in _rowByKey.Keys.Where(key => !alive.Contains(key)).ToList())
        {
            _rowByKey.Remove(key);
        }

        UpdateEmptyState();
        Rebuilt?.Invoke(this, EventArgs.Empty);
        if (axisChanged)
        {
            AxisChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>案件の実績の合計と実績の棒を総合計に足す</summary>
    /// <param name="project">案件の行 (合計は隠した作業も含めて計算済み)</param>
    /// <param name="snapshot">スケジュールに出す内容 (実績の棒を引く)</param>
    private void AddProjectTotal(WorkNode project, ScheduleSnapshot snapshot)
    {
        RemoveProjectTotal(project.Key.Id);
        var added = (project.Summary.ActualTotal, snapshot.Actuals.TryGetValue(project.Key, out var runs) ? runs : []);
        _projectTotals[project.Key.Id] = added;
        _total.Add(added.Item1, added.Item2);
    }

    /// <summary>足してあった案件の分を総合計から引く</summary>
    /// <param name="projectId">案件の番号</param>
    /// <remarks>足していない案件なら、何もしない。</remarks>
    private void RemoveProjectTotal(int projectId)
    {
        if (_projectTotals.Remove(projectId, out var removed))
        {
            _total.Remove(removed.Hours, removed.Runs);
        }
    }

    /// <summary>総合計の行を今の総合計の値に合わせる</summary>
    private void UpdateTotalRow() => TotalRow.UpdateTotal(_total.Hours, _total.Runs(), Axis!, _service.Today);

    /// <summary>木の行から、表の行を使い回しで作り、見えている行を木の順に並べる</summary>
    /// <param name="node">木の行</param>
    /// <param name="level">字下げの段</param>
    /// <param name="snapshot">スケジュールに出す内容 (実績の棒を引く)</param>
    /// <param name="target">見えている行の並び (ここに足す)</param>
    /// <param name="alive">使っている行の識別 (ここに足す)</param>
    private void Visit(WorkNode node, int level, ScheduleSnapshot snapshot, List<ScheduleRow> target, HashSet<WorkKey> alive)
    {
        if (!_rowByKey.TryGetValue(node.Key, out var row))
        {
            row = new ScheduleRow(node.Key);
            _rowByKey[node.Key] = row;
        }

        var expanded = !_collapsed.Contains(node.Key);
        row.Update(
            node,
            level,
            node.Children.Count > 0,
            expanded,
            node.Item is { } work ? _service.AssigneeName(work.AssigneeUserId) : "",
            _service.Codes,
            snapshot.Actuals.TryGetValue(node.Key, out var runs) ? runs : [],
            snapshot.DayNotes.TryGetValue(node.Key, out var dayNotes) ? dayNotes : ScheduleRow.NoDayNotes,
            Axis!,
            _service.Today);
        alive.Add(node.Key);
        target.Add(row);
        if (expanded)
        {
            foreach (var child in node.Children)
            {
                Visit(child, level + 1, snapshot, target, alive);
            }
        }
        else
        {
            // 閉じている間も、行は使い回せるよう残す
            foreach (var child in node.Children.SelectMany(c => c.SelfAndDescendants()))
            {
                alive.Add(child.Key);
            }
        }
    }

    /// <summary>表示する案件の変更にその案件の行だけで応える</summary>
    /// <param name="args">変更の内容</param>
    /// <remarks>
    /// できないときは全体を作り直す。
    /// 案件の中身はほかの案件に依存しない。
    /// そのため、外した案件はその行のかたまりを抜くだけ、加えた案件はその案件の分だけを作って、名前の順の位置へ差し込めばよい。
    /// 日付の軸は外したときは変えない。
    /// 縮めないで、開き直したときに、データに合わせる。
    /// 加えたときは軸の範囲に収まるときだけ差し込み、はみ出すときは全体を作り直す。
    /// </remarks>
    private void OnServiceChanged(WorkChangedEventArgs args)
    {
        var handled = _active && _loadCount == 0 && Axis is not null && args.Kind switch
        {
            WorkChangeKind.ProjectHidden => RemoveProjectRows(args.ProjectId),
            WorkChangeKind.ProjectShown => InsertProjectRows(args.ProjectId),
            _ => false,
        };
        if (!handled)
        {
            Rebuild();
        }
    }

    /// <summary>外した案件の行のかたまりを表から抜く</summary>
    /// <param name="projectId">案件の番号</param>
    /// <returns>反映できたら true (表に出ていない案件は反映できなかったことにして、全体を作り直す)。</returns>
    private bool RemoveProjectRows(int projectId)
    {
        if (ProjectBlockRows.Remove(Rows, projectId) is not { } removed)
        {
            return false;
        }

        foreach (var row in removed)
        {
            _rowByKey.Remove(row.Key);
        }
        RemoveProjectTotal(projectId);
        UpdateTotalRow();
        UpdateEmptyState();
        RowsBlockChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>加えた案件の行のかたまりを作り、案件の名前の順の位置へ差し込む</summary>
    /// <param name="projectId">案件の番号</param>
    /// <returns>反映できたら true (日付の軸の範囲に収まらないとき・案件が無いときは false)</returns>
    private bool InsertProjectRows(int projectId)
    {
        var key = new WorkKey(WorkRowKind.Project, projectId);
        if (_rowByKey.ContainsKey(key))
        {
            return false;
        }

        var snapshot = _service.BuildScheduleOfProject(projectId, MineOnly, Completed);
        var axis = Axis!.Axis;
        if (snapshot.Roots.Count != 1 || snapshot.AxisStart < axis.Start || snapshot.AxisEnd > axis.End)
        {
            return false;
        }

        var target = new List<ScheduleRow>();
        Visit(snapshot.Roots[0], 0, snapshot, target, []);

        ProjectBlockRows.Insert(Rows, target);
        AddProjectTotal(snapshot.Roots[0], snapshot);
        UpdateTotalRow();
        UpdateEmptyState();
        RowsBlockChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>完了した作業の見え方で、選択肢の番号から求める</summary>
    /// <remarks>範囲外の値は「非表示」</remarks>
    private CompletedDisplay Completed => Enum.IsDefined((CompletedDisplay)CompletedIndex) ? (CompletedDisplay)CompletedIndex : CompletedDisplay.Hidden;

    /// <summary>行が無いときの案内を今の行に合わせる</summary>
    private void UpdateEmptyState()
    {
        IsEmpty = Rows.Count == 0;
        EmptyMessage = !IsEmpty || _service.HasVisibleProjects ? "" : _masterEmpty ? "「案件の管理」から、案件を追加してください" : "表示する案件を選んでください";
    }
}
