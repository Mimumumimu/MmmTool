using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Controls;
using MmmTool.WorkItems.Core;
using MmmTool.WorkItems.ProjectPicker;

namespace MmmTool.WorkItems.Schedule;

/// <summary>
/// スケジュールのページ。作業の予定と実績を、日付を横に並べて見る (見るだけ)。
/// </summary>
/// <remarks>
/// 案件・グループ・作業と、他の人の更新の取り込みは、作業管理と同じ <see cref="WorkItemService"/> を使う。
/// 実績 (すべての日の記録)は、ページを開くたびに読み、離れたら捨てる (<see cref="InitializeAsync"/>・<see cref="Suspend"/>)。
/// 「表示する案件」「自分の担当だけ」は、作業管理と同じ設定を共有する。
/// </remarks>
public sealed partial class ScheduleViewModel : ObservableObject
{
    /// <summary>作業管理の読み書き</summary>
    private readonly WorkItemService _service;

    /// <summary>設定ストア</summary>
    private readonly ISettingsStore _settings;

    /// <summary>行の識別から、表の行を引く (使い回す)</summary>
    private readonly Dictionary<WorkKey, ScheduleRow> _rowByKey = [];

    /// <summary>利用者が閉じた案件・グループ (既定では、すべて開く)</summary>
    private readonly HashSet<WorkKey> _collapsed = [];

    /// <summary>ページを開いている間か (離れている間は、サービスの変更で表を作り直さない)</summary>
    private bool _active;

    /// <summary>作っている最中か (設定の読み込みで、値の変更の処理を動かさないため)</summary>
    private bool _initializing = true;

    /// <summary>案件マスターが空か (表示する案件が無いときの案内を決める)</summary>
    private bool _masterEmpty;

    /// <summary>表の行 (見えている行を 1 つにならしたもの。木の順)</summary>
    public ObservableCollection<ScheduleRow> Rows { get; } = [];

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>表示する案件を選ぶ画面の ViewModel (作業管理と同じ設定を使う)</summary>
    public ProjectPickerViewModel ProjectPicker { get; }

    /// <summary>担当者を扱うか (ローカルモードは false。担当の列と絞り込みを出さない)</summary>
    public bool HasAssignees => _service.HasAssignees;

    /// <summary>自分が担当の作業だけを表示するか (作業管理と同じ設定)</summary>
    [ObservableProperty]
    public partial bool MineOnly { get; set; }

    /// <summary>表示の単位の番号 (<see cref="ScheduleUnit"/> の値と同じ。保存しない。開くたびに日)</summary>
    [ObservableProperty]
    public partial int UnitIndex { get; set; } = (int)ScheduleUnit.Day;

    /// <summary>行が 1 つも無いか (案内を出す)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>行が無いときの案内 (表示する案件を選ぶか、案件を追加するか)</summary>
    [ObservableProperty]
    public partial string EmptyMessage { get; private set; } = "";

    /// <summary>今日の日付</summary>
    public DateOnly Today => _service.Today;

    /// <summary>今の日付の軸 (範囲と単位。まだ作っていなければ null)</summary>
    public ScheduleAxisVisual? Axis { get; private set; }

    /// <summary>日付の軸が変わった (範囲・単位が変わった。列の幅と見出しを作り直すため)</summary>
    public event EventHandler? AxisChanged;

    /// <summary>ViewModel を作る</summary>
    /// <param name="service">作業管理の読み書き</param>
    /// <param name="settings">設定ストア</param>
    /// <param name="projectPicker">表示する案件を選ぶ画面の ViewModel</param>
    public ScheduleViewModel(WorkItemService service, ISettingsStore settings, ProjectPickerViewModel projectPicker)
    {
        _service = service;
        _settings = settings;
        ProjectPicker = projectPicker;

        service.Changed += (_, _) => Rebuild();
        service.RefreshFailed += (_, message) => Error.Show(message);
        MineOnly = settings.Get(WorkItemSettingKeys.MineOnly, false);
        _initializing = false;
    }

    /// <summary>ページを開いたときに、保存先から読み直して表を作り、他の人の更新の取得を始める</summary>
    /// <returns>初期化の完了を表すタスク</returns>
    /// <remarks>作業管理の表の入力日をずらさないよう、記録を読んでいる日 (<see cref="WorkItemService.Day"/>)のまま読み直す。</remarks>
    public async Task InitializeAsync()
    {
        Error.Clear();

        // 「自分の担当だけ」は、作業管理でも切り替えられる (同じ設定)。画面を開くたびに、設定の値に合わせる
        var mineOnly = _settings.Get(WorkItemSettingKeys.MineOnly, false);
        if (mineOnly != MineOnly)
        {
            _initializing = true;
            MineOnly = mineOnly;
            _initializing = false;
        }

        _active = true;
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
        Rebuild();
        _service.StartPolling();
    }

    /// <summary>ページを離れたときに、他の人の更新の取得を止め、実績を捨てる</summary>
    public void Suspend()
    {
        _active = false;
        _service.StopPolling();
        _service.ReleaseScheduleRecords();
    }

    /// <summary>案件・グループを、開く・閉じる</summary>
    /// <param name="row">押された行</param>
    public void Toggle(ScheduleRow row)
    {
        if (!_collapsed.Remove(row.Key))
        {
            _collapsed.Add(row.Key);
        }
        Rebuild();
    }

    /// <summary>自分の担当だけを表示するかが変わったら、設定に残して、表を作り直す (読み込みは、やり直さない)</summary>
    /// <param name="value">自分の担当だけにするか</param>
    partial void OnMineOnlyChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _settings.SetAsync(WorkItemSettingKeys.MineOnly, value).Forget();
        Rebuild();
    }

    /// <summary>表示の単位が変わったら、軸を作り直す</summary>
    /// <param name="value">単位の番号</param>
    partial void OnUnitIndexChanged(int value)
    {
        if (!_initializing)
        {
            Rebuild();
        }
    }

    /// <summary>サービスの内容から、軸と行の一覧を作り直す (同じ行は使い回し、差分だけを一覧に反映する)</summary>
    private void Rebuild()
    {
        if (!_active)
        {
            return;
        }

        var snapshot = _service.BuildSchedule(MineOnly);
        var unit = Enum.IsDefined((ScheduleUnit)UnitIndex) ? (ScheduleUnit)UnitIndex : ScheduleUnit.Day;
        var axis = ScheduleAxis.Create(snapshot.AxisStart, snapshot.AxisEnd, unit);
        var axisChanged = Axis is null || Axis.Axis != axis;
        if (axisChanged)
        {
            Axis = new ScheduleAxisVisual(axis);
        }

        var today = _service.Today;
        var target = new List<ScheduleRow>();
        var alive = new HashSet<WorkKey>();

        void Visit(WorkNode node, int level)
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
                snapshot.Actuals.TryGetValue(node.Key, out var runs) ? runs : [],
                Axis!,
                today);
            alive.Add(node.Key);
            target.Add(row);
            if (expanded)
            {
                foreach (var child in node.Children)
                {
                    Visit(child, level + 1);
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

        foreach (var root in snapshot.Roots)
        {
            Visit(root, 0);
        }

        TreeGridRowSync.Sync(Rows, target);
        foreach (var key in _rowByKey.Keys.Where(key => !alive.Contains(key)).ToList())
        {
            _rowByKey.Remove(key);
        }

        IsEmpty = Rows.Count == 0;
        EmptyMessage = !IsEmpty || _service.HasVisibleProjects ? "" : _masterEmpty ? "「案件の管理」から、案件を追加してください" : "表示する案件を選んでください";
        if (axisChanged)
        {
            AxisChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
