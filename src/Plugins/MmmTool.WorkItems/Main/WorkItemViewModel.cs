using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.WorkItems.Core;
using MmmTool.WorkItems.Move;
using MmmTool.WorkItems.ProjectPicker;

namespace MmmTool.WorkItems.Main;

/// <summary>
/// 作業管理のページ。木構造の表・入力日・表示列・表示する案件・完了した行を隠すかを扱う。
/// </summary>
/// <remarks>
/// 表のセルへの入力は、確定したとき (フォーカスが外れたとき・Enter)に、その場で保存する (<c>Commit～</c>)。入力が受け入れられなかったときは、
/// 理由を <see cref="Error"/> に出し、入力欄を保存されている値に戻す。案件・グループの開閉・非表示の列・過去の完了を表示するかは、この PC の設定 (設定ストア)に持つ。
/// 画面に出すのは、利用者が選んだ案件だけ。他の人の更新は、サービスが 60 秒ごとに取り込み、<see cref="WorkItemService.Changed"/> で表を作り直す。
/// </remarks>
public sealed partial class WorkItemViewModel : ObservableObject
{
    /// <summary>非表示の列を、設定ストアに持つキー (列のキーを、カンマでつないだ文字列)</summary>
    private const string HiddenColumnsKey = "WorkItems.HiddenColumns";

    /// <summary>完了した行を表示するかを、設定ストアに持つキー (真偽)</summary>
    private const string ShowFinishedKey = "WorkItems.ShowFinished";

    /// <summary>自分の担当だけを表示するかの設定のキー (この PC の設定)</summary>
    private const string MineOnlyKey = "WorkItems.MineOnly";

    /// <summary>日付として受け付ける書式</summary>
    private static readonly string[] DateFormats = ["yyyy/M/d", "yyyy-M-d", "yyyy.M.d", "yyyyMMdd", "M/d", "M-d"];

    /// <summary>作業管理の読み書き</summary>
    private readonly WorkItemService _service;

    /// <summary>設定ストア</summary>
    private readonly ISettingsStore _settings;

    /// <summary>作業管理のダイアログ</summary>
    private readonly IWorkItemDialogService _dialogs;

    /// <summary>確認ダイアログ</summary>
    private readonly IDialogService _confirm;

    /// <summary>行の識別から、表の行を引く (使い回す)</summary>
    private readonly Dictionary<WorkKey, WorkItemRow> _rowByKey = [];

    /// <summary>利用者が閉じた案件・グループ</summary>
    private readonly HashSet<WorkKey> _collapsed = [];

    /// <summary>利用者が開いた案件・グループ (既定で畳む、完了したグループを、開いたまま保つため)</summary>
    private readonly HashSet<WorkKey> _expanded = [];

    /// <summary>非表示の列のキー</summary>
    private readonly HashSet<string> _hiddenColumns;

    /// <summary>選択欄の選択肢 (コードマスタが変わらない間は、同じ一覧を使い回す)</summary>
    private WorkChoices _choices = new(MmmTool.CodeMaster.Core.CodeBook.Empty);

    /// <summary>壊れたファイルを退避した知らせを、もう出したか (サービスが、アプリの実行中ずっと持つので、ページを開くたびには出さない)</summary>
    private bool _recoveryMessageShown;

    /// <summary>案件マスターが空か (表示する案件が無いときの案内を決める)</summary>
    private bool _masterEmpty;

    /// <summary>作っている最中か (設定の読み込みで、値の変更の処理を動かさないため)</summary>
    private bool _initializing = true;

    /// <summary>表の行 (見えている行を 1 つにならしたもの。木の順)</summary>
    public ObservableCollection<WorkItemRow> Rows { get; } = [];

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>表示する案件を選ぶ画面の ViewModel</summary>
    public ProjectPickerViewModel ProjectPicker { get; }

    /// <summary>入力日 (実績 (日)・備考 (日)の日。既定は今日)</summary>
    [ObservableProperty]
    public partial DateOnly InputDate { get; set; }

    /// <summary>完了して日がたった行も表示するか</summary>
    [ObservableProperty]
    public partial bool ShowFinished { get; set; }

    /// <summary>担当者を扱うか (ローカルモードは false。担当の列と絞り込みを出さない)</summary>
    public bool HasAssignees => _service.HasAssignees;

    /// <summary>自分が担当の作業だけを表示するか</summary>
    [ObservableProperty]
    public partial bool MineOnly { get; set; }

    /// <summary>担当者に選べる人 (先頭は未割り当て、次が自分、そのあとは表示名の順)</summary>
    public IReadOnlyList<WorkAssignee> AssigneeChoices
    {
        get
        {
            var selectable = _service.Assignees.Where(a => a.IsSelectable).ToList();
            return
            [
                new WorkAssignee(0, "未割り当て", true),
                .. selectable.Where(a => a.UserId == _service.UserId),
                .. selectable.Where(a => a.UserId != _service.UserId),
            ];
        }
    }

    /// <summary>行が 1 つも無いか (案内を出す)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>行が無いときの案内 (表示する案件を選ぶか、案件を追加するか)</summary>
    [ObservableProperty]
    public partial string EmptyMessage { get; private set; } = "";

    /// <summary>選んだ行</summary>
    [ObservableProperty]
    public partial WorkItemRow? SelectedRow { get; set; }

    /// <summary>追加した行の名前を、入力できる状態にしてほしい</summary>
    public event EventHandler<WorkItemRow>? FocusNameRequested;

    /// <summary>表示する列が変わった (起動時の読み込みを除く)</summary>
    public event EventHandler? ColumnVisibilityChanged;

    /// <summary>行の一覧を作り直した (列の幅を内容に合わせ直すため)</summary>
    public event EventHandler? RowsRebuilt;

    /// <summary>ViewModel を作る</summary>
    /// <param name="service">作業管理の読み書き</param>
    /// <param name="settings">設定ストア</param>
    /// <param name="dialogs">作業管理のダイアログ</param>
    /// <param name="confirm">確認ダイアログ</param>
    /// <param name="projectPicker">表示する案件を選ぶ画面の ViewModel</param>
    public WorkItemViewModel(WorkItemService service, ISettingsStore settings, IWorkItemDialogService dialogs, IDialogService confirm, ProjectPickerViewModel projectPicker)
    {
        _service = service;
        _settings = settings;
        _dialogs = dialogs;
        _confirm = confirm;
        ProjectPicker = projectPicker;

        _hiddenColumns = [.. settings.Get(HiddenColumnsKey, "").Split(',', StringSplitOptions.RemoveEmptyEntries)];
        service.Changed += (_, _) => Rebuild();
        service.RefreshFailed += (_, message) => Error.Show(message);

        // 値の変更で表を作り直す処理が動くので、上の準備のあとに設定する
        ShowFinished = settings.Get(ShowFinishedKey, false);
        MineOnly = settings.Get(MineOnlyKey, false);
        InputDate = service.Today;
        _initializing = false;
    }

    /// <summary>ページを開いたときに、保存先から読み直して表を作り、他の人の更新の取得を始める</summary>
    /// <returns>初期化の完了を表すタスク</returns>
    public async Task InitializeAsync()
    {
        Error.Clear();
        try
        {
            await _service.LoadAsync(InputDate);
            if (!_recoveryMessageShown && _service.RecoveryMessage is { } recovery)
            {
                _recoveryMessageShown = true;
                Error.Show(recovery);
            }
            await RefreshMasterAsync();
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        Rebuild();
        _service.StartPolling();
    }

    /// <summary>ページを離れたときに、他の人の更新の取得を止める</summary>
    public void Suspend() => _service.StopPolling();

    /// <summary>列が、表示されているか</summary>
    /// <param name="key">列のキー</param>
    /// <returns>表示していれば true</returns>
    public bool IsColumnVisible(string key) => !_hiddenColumns.Contains(key);

    /// <summary>列の表示・非表示を切り替えて、設定に残す</summary>
    /// <param name="key">列のキー</param>
    /// <param name="isVisible">表示するか</param>
    public void SetColumnVisible(string key, bool isVisible)
    {
        if (isVisible ? !_hiddenColumns.Remove(key) : !_hiddenColumns.Add(key))
        {
            return;
        }

        _settings.SetAsync(HiddenColumnsKey, string.Join(',', _hiddenColumns)).Forget();
        ColumnVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>入力日を、前の日にする</summary>
    [RelayCommand]
    private void PreviousDay() => InputDate = InputDate.AddDays(-1);

    /// <summary>入力日を、次の日にする</summary>
    [RelayCommand]
    private void NextDay() => InputDate = InputDate.AddDays(1);

    /// <summary>入力日を、今日にする</summary>
    [RelayCommand]
    private void Today() => InputDate = _service.Today;

    /// <summary>入力日が変わったら、その日の記録だけを読み直す</summary>
    /// <param name="value">新しい入力日</param>
    partial void OnInputDateChanged(DateOnly value)
    {
        if (!_initializing)
        {
            ChangeDayAsync(value).Forget();
        }
    }

    /// <summary>自分の担当だけを表示するかが変わったら、設定に残して、表を作り直す (読み込みは、やり直さない)</summary>
    /// <param name="value">自分の担当だけにするか</param>
    partial void OnMineOnlyChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _settings.SetAsync(MineOnlyKey, value).Forget();
        Rebuild();
    }

    /// <summary>担当者を、確定する</summary>
    /// <param name="row">行 (作業)</param>
    /// <param name="userId">新しい担当者の番号 (0 は未割り当て)</param>
    /// <returns>確定の完了を表すタスク</returns>
    public async Task CommitAssigneeAsync(WorkItemRow row, int userId)
    {
        if (userId != row.AssigneeUserId)
        {
            await UpdateItemAsync(row, item => item with { AssigneeUserId = userId });
        }
    }

    /// <summary>過去の完了を表示するかが変わったら、設定に残して、表を作り直す (読み込みは、やり直さない)</summary>
    /// <param name="value">表示するか</param>
    partial void OnShowFinishedChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        _settings.SetAsync(ShowFinishedKey, value).Forget();
        Rebuild();
    }

    /// <summary>案件・グループを開く・閉じる (開閉の状態は、保存しない)</summary>
    /// <param name="row">開閉する行</param>
    public void Toggle(WorkItemRow row)
    {
        if (row.IsExpanded)
        {
            _expanded.Remove(row.Key);
            _collapsed.Add(row.Key);
        }
        else
        {
            _collapsed.Remove(row.Key);
            _expanded.Add(row.Key);
        }
        Rebuild();
    }

    /// <summary>案件・グループを、開いた状態にする</summary>
    /// <param name="key">開く行の識別</param>
    /// <returns>表示が変わる (閉じていた)なら true</returns>
    /// <remarks>既定で畳むグループも、利用者が開いたものとして覚える。作業を足して未完了に戻っても、開いたままにするため。</remarks>
    private bool Open(WorkKey key)
    {
        var wasOpen = _rowByKey.TryGetValue(key, out var row) && row.IsExpanded;
        _collapsed.Remove(key);
        _expanded.Add(key);
        return !wasOpen;
    }

    /// <summary>行を追加する</summary>
    /// <param name="kind">追加する行の種類</param>
    /// <param name="target">右クリックした行 (案件・グループなら中に、作業なら同じ階層の次に追加する。案件を追加するときは null)</param>
    /// <returns>追加の完了を表すタスク</returns>
    public async Task AddAsync(WorkRowKind kind, WorkItemRow? target)
    {
        try
        {
            WorkAddResult result;
            if (kind == WorkRowKind.Project || target is null)
            {
                result = await _service.AddProjectAsync("新しい案件");
                _masterEmpty = false;
            }
            else
            {
                var (projectId, parentGroupId, after) = target.Kind switch
                {
                    WorkRowKind.Project => (target.Id, 0, (WorkKey?)null),
                    WorkRowKind.Group when _service.GetGroup(target.Id) is { } group => (group.ProjectId, group.Id, null),
                    WorkRowKind.Work when _service.GetItem(target.Id) is { } item => (item.ProjectId, item.GroupId, target.Key),
                    _ => (0, 0, null),
                };
                if (projectId == 0)
                {
                    Error.Show("追加先は、すでに削除されています。");
                    return;
                }
                if (target.IsGroup)
                {
                    Open(target.Key);
                }

                result = await _service.AddAsync(kind, projectId, parentGroupId, after);
            }

            if (result.Error is { } error)
            {
                Error.Show(error);
                return;
            }

            Error.Clear();
            if (result.Key is { } key && _rowByKey.TryGetValue(key, out var row))
            {
                SelectedRow = row;
                FocusNameRequested?.Invoke(this, row);
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>行を、兄弟の中で上または下へ動かせるか</summary>
    /// <param name="row">行</param>
    /// <param name="delta">動かす数 (-1 で上、1 で下)</param>
    /// <returns>動かせれば true (案件は、並びを動かせない)</returns>
    public bool CanShift(WorkItemRow row, int delta) => !row.IsProject && _service.CanShift(row.Key, delta);

    /// <summary>行を、兄弟の中で上または下へ動かす</summary>
    /// <param name="row">行</param>
    /// <param name="delta">動かす数 (-1 で上、1 で下)</param>
    /// <returns>動かしの完了を表すタスク</returns>
    public async Task ShiftAsync(WorkItemRow row, int delta)
    {
        try
        {
            await _service.ShiftAsync(row.Key, delta);
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>移動先を選んで、行 (グループなら中身ごと)を移す</summary>
    /// <param name="row">移す行</param>
    /// <returns>移動の完了を表すタスク</returns>
    public async Task MoveAsync(WorkItemRow row)
    {
        var destinations = new List<MoveDestination>();
        foreach (var project in _service.BuildTree(InputDate))
        {
            destinations.Add(new MoveDestination(project.Key.Id, 0, project.Name, 0));
            AddDestinations(project.Children, project.Key.Id, row.Key, 1, destinations);
        }
        if (await _dialogs.PickDestinationAsync(destinations) is not { } destination)
        {
            return;
        }

        try
        {
            var error = await _service.MoveAsync(row.Key, destination.ProjectId, destination.GroupId);
            if (error is not null)
            {
                Error.Show(error);
                return;
            }

            Error.Clear();
            var destinationKey = destination.GroupId != 0
                ? new WorkKey(WorkRowKind.Group, destination.GroupId)
                : new WorkKey(WorkRowKind.Project, destination.ProjectId);
            if (Open(destinationKey))
            {
                Rebuild();
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>確認してから、行を削除する (論理削除。グループは中身も一緒に)</summary>
    /// <param name="row">削除する行 (グループまたは作業。案件は、案件の管理の画面で削除する)</param>
    /// <returns>削除の完了を表すタスク</returns>
    public async Task DeleteAsync(WorkItemRow row)
    {
        try
        {
            var message = row.IsGroup ? $"グループ「{row.Name}」を削除します。" : $"作業「{row.Name}」を削除します。";
            if (!await _confirm.ConfirmAsync("削除の確認", message, "削除", "キャンセル"))
            {
                return;
            }

            var error = await _service.DeleteAsync(row.Key);
            if (error is null)
            {
                Error.Clear();
            }
            else
            {
                Error.Show(error);
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>案件の管理の画面を開く (バーの「案件の管理」から)</summary>
    /// <returns>画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private async Task OpenProjectManagerAsync()
    {
        await _dialogs.ShowProjectManagerAsync();
        try
        {
            await RefreshMasterAsync();
            await ProjectPicker.RefreshAsync();
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        Rebuild();
    }

    /// <summary>文字の入力欄 (名前・日付・時間)の入力を、確定する</summary>
    /// <param name="row">行</param>
    /// <param name="column">列のキー (Name / Start / Due / Planned / ActualDay)</param>
    /// <param name="text">入力された文字</param>
    /// <returns>確定の完了を表すタスク</returns>
    public async Task CommitTextAsync(WorkItemRow row, string column, string text)
    {
        text = text.Trim();
        if (column == "Name" && text.Length == 0)
        {
            // 入力欄の使い回しの途中で、空の確定が来ることがある。名前は必須なので、保存せず、元に戻す
            row.Revert();
            return;
        }
        var current = column switch
        {
            "Name" => row.Name,
            "Start" => row.StartText,
            "Due" => row.DueText,
            "Planned" => row.PlannedText,
            "ActualDay" => row.ActualDayText,
            _ => text,
        };
        if (text == current)
        {
            return;
        }

        switch (column)
        {
            case "Name":
                await UpdateTextAsync(row, name: text, note: null);
                break;
            case "Start":
            case "Due":
                if (!TryParseDate(text, out var date))
                {
                    Reject(row, "日付は、2026/10/10 の形で入力してください。");
                    return;
                }
                await UpdateItemAsync(row, item => column == "Start" ? item with { StartDate = date } : item with { DueDate = date });
                break;
            case "Planned":
                if (!TryParseHours(text, out var planned))
                {
                    Reject(row, "予定時間は、数字で入力してください。");
                    return;
                }
                await UpdateItemAsync(row, item => item with { PlannedHours = planned });
                break;
            case "ActualDay":
                if (!TryParseHours(text, out var hours))
                {
                    Reject(row, "実績は、数字で入力してください。");
                    return;
                }
                await SetRecordAsync(row, hours ?? 0, _service.GetRecord(row.Id, InputDate)?.Note ?? "");
                break;
        }
    }

    /// <summary>備考の入力を、確定する</summary>
    /// <param name="row">行</param>
    /// <param name="column">列のキー (Note / DayNote)</param>
    /// <param name="text">入力された文章</param>
    /// <returns>確定の完了を表すタスク</returns>
    public async Task CommitNoteAsync(WorkItemRow row, string column, string text)
    {
        if (column == "DayNote")
        {
            if (row.IsWork && text != row.DayNote)
            {
                await SetRecordAsync(row, _service.GetRecord(row.Id, InputDate)?.Hours ?? 0, text);
            }
        }
        else if (text != row.Note)
        {
            await UpdateTextAsync(row, name: null, note: text);
        }
    }

    /// <summary>備考の入力欄に出す全文を返す</summary>
    /// <param name="row">行</param>
    /// <param name="column">列のキー (Note / DayNote)</param>
    /// <returns>備考の全文</returns>
    public static string GetNote(WorkItemRow row, string column) => column == "DayNote" ? row.DayNote : row.Note;

    /// <summary>選択欄 (優先度・状態・進捗度)の選択を、確定する</summary>
    /// <param name="row">行</param>
    /// <param name="column">列のキー (Priority / Status / Progress)</param>
    /// <param name="index">選択位置</param>
    /// <returns>確定の完了を表すタスク</returns>
    public async Task CommitChoiceAsync(WorkItemRow row, string column, int index)
    {
        if (index < 0)
        {
            return;
        }

        switch (column)
        {
            case "Priority" when index < row.Choices.Priorities.Count && index != row.PriorityIndex:
                await UpdateItemAsync(row, item => item with { Priority = (WorkItemPriority)row.Choices.Priorities[index].CodeNo });
                break;
            case "Status" when index < row.Choices.Statuses.Count && index != row.StatusIndex:
                await UpdateItemAsync(row, item => item with { Status = (WorkItemStatus)row.Choices.Statuses[index].CodeNo });
                break;
            case "Progress" when index < row.Choices.Progresses.Count && index != row.ProgressIndex:
                await UpdateItemAsync(row, item => item with { ProgressCodeNo = row.Choices.Progresses[index].CodeNo });
                break;
        }
    }

    /// <summary>名前または備考を、行の種類に合わせて更新する</summary>
    /// <param name="row">行</param>
    /// <param name="name">新しい名前 (変えないなら null)</param>
    /// <param name="note">新しい備考 (変えないなら null)</param>
    /// <returns>更新の完了を表すタスク</returns>
    private async Task UpdateTextAsync(WorkItemRow row, string? name, string? note)
    {
        try
        {
            string? error;
            switch (row.Kind)
            {
                case WorkRowKind.Project:
                    error = await _service.UpdateProjectAsync(row.Id, name, note);
                    break;
                case WorkRowKind.Group when _service.GetGroup(row.Id) is { } group:
                    error = await _service.UpdateGroupAsync(group with { Name = name ?? group.Name, Note = note ?? group.Note });
                    break;
                case WorkRowKind.Work when _service.GetItem(row.Id) is { } item:
                    error = await _service.UpdateItemAsync(item with { Name = name ?? item.Name, Note = note ?? item.Note }, InputDate);
                    break;
                default:
                    return;
            }
            AcceptOrReject(row, error);
        }
        catch (DataFileException ex)
        {
            Reject(row, ex.Message);
        }
    }

    /// <summary>作業の項目を更新する</summary>
    /// <param name="row">行 (作業)</param>
    /// <param name="change">今の作業から、更新後の作業を作る処理</param>
    /// <returns>更新の完了を表すタスク</returns>
    private async Task UpdateItemAsync(WorkItemRow row, Func<WorkItem, WorkItem> change)
    {
        if (_service.GetItem(row.Id) is not { } item)
        {
            return;
        }

        try
        {
            AcceptOrReject(row, await _service.UpdateItemAsync(change(item), InputDate));
        }
        catch (DataFileException ex)
        {
            Reject(row, ex.Message);
        }
    }

    /// <summary>日ごとの記録を保存する</summary>
    /// <param name="row">行</param>
    /// <param name="hours">入力日の実績</param>
    /// <param name="note">入力日の備考</param>
    /// <returns>保存の完了を表すタスク</returns>
    private async Task SetRecordAsync(WorkItemRow row, double hours, string note)
    {
        try
        {
            AcceptOrReject(row, await _service.SetRecordAsync(row.Id, InputDate, hours, note));
        }
        catch (DataFileException ex)
        {
            Reject(row, ex.Message);
        }
    }

    /// <summary>保存の結果に合わせて、エラーを消すか、入力を受け入れずに戻す</summary>
    /// <param name="row">行</param>
    /// <param name="error">入力の誤りのメッセージ。保存できたときは null</param>
    private void AcceptOrReject(WorkItemRow row, string? error)
    {
        if (error is null)
        {
            Error.Clear();
        }
        else
        {
            Reject(row, error);
        }
    }

    /// <summary>入力を受け入れず、理由を出して、入力欄を保存されている値に戻す</summary>
    /// <param name="row">行</param>
    /// <param name="message">理由</param>
    private void Reject(WorkItemRow row, string message)
    {
        Error.Show(message);
        row.Revert();
    }

    /// <summary>入力日を変えたあと、その日の記録を読み直す</summary>
    /// <param name="day">新しい入力日</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    private async Task ChangeDayAsync(DateOnly day)
    {
        try
        {
            await _service.SetDayAsync(day);
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        Rebuild();
    }

    /// <summary>案件マスターが空かを確かめる</summary>
    /// <returns>確認の完了を表すタスク</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    private async Task RefreshMasterAsync() => _masterEmpty = (await _service.SearchProjectsAsync("", false, false, 0, 1)).Count == 0;

    /// <summary>木構造から、見えている行の一覧を作り直す</summary>
    /// <remarks>同じ行は使い回して値だけを書き換え、行の増減・並びの変更は、差分だけを一覧に反映する (入力欄の位置とフォーカスを保つため)。</remarks>
    private void Rebuild()
    {
        if (!ReferenceEquals(_choices.Book, _service.Codes))
        {
            _choices = new WorkChoices(_service.Codes);
        }

        var target = new List<WorkItemRow>();
        var alive = new HashSet<WorkKey>();

        // 完了して日がたった (完了日が入力日より前の)もの
        bool IsPastFinished(WorkNode node)
            => node.Summary.IsFinished && node.Summary.CompletedDate is { } done && done < InputDate;

        // 隠すのは、完了して日がたった作業だけ。案件・グループは隠さない (追加先を探せるように)。集計は隠した行も含んだまま
        bool IsHidden(WorkNode node)
            => !ShowFinished && node.Key.Kind == WorkRowKind.Work && IsPastFinished(node);

        // 中身がすべて完了して日がたったグループは、既定で畳む (利用者が開いたら開く)。作業が 1 件も無いグループ (空)が下にあるときは畳まない (消えたように見えないため)
        bool IsCollapsedByDefault(WorkNode node)
            => node.Key.Kind == WorkRowKind.Group && !ShowFinished && IsPastFinished(node)
            && !node.Children.SelectMany(c => c.SelfAndDescendants()).Any(n => n.Key.Kind == WorkRowKind.Group && n.Summary.WorkCount == 0);

        bool IsExpanded(WorkNode node)
            => _expanded.Contains(node.Key) || (!_collapsed.Contains(node.Key) && !IsCollapsedByDefault(node));

        void Visit(WorkNode node, int level)
        {
            // 完了して日がたったグループは、隠した作業があっても三角を出し、利用者が開いたときは、隠した作業も出す
            var hasHidden = node.Key.Kind == WorkRowKind.Group && !ShowFinished && IsPastFinished(node);
            var reveal = hasHidden && _expanded.Contains(node.Key);
            var visibleChildren = reveal ? node.Children.ToList() : node.Children.Where(c => !IsHidden(c)).ToList();
            if (!_rowByKey.TryGetValue(node.Key, out var row))
            {
                row = new WorkItemRow(node.Key);
                _rowByKey[node.Key] = row;
            }

            var expanded = IsExpanded(node);
            row.Update(
                node,
                level,
                visibleChildren.Count > 0 || (hasHidden && node.Children.Count > 0),
                expanded,
                node.Item is { } item ? _service.GetRecord(item.Id, InputDate) : null,
                _choices,
                node.Item is { } work ? _service.AssigneeName(work.AssigneeUserId) : "");
            alive.Add(node.Key);
            target.Add(row);
            if (expanded)
            {
                foreach (var child in visibleChildren)
                {
                    Visit(child, level + 1);
                }
            }
            else
            {
                // 閉じている間も、行は使い回せるよう残す
                foreach (var child in visibleChildren.SelectMany(c => c.SelfAndDescendants()))
                {
                    alive.Add(child.Key);
                }
            }
        }

        foreach (var root in _service.BuildTree(InputDate, MineOnly))
        {
            Visit(root, 0);
        }

        for (var i = 0; i < target.Count; i++)
        {
            if (i < Rows.Count && ReferenceEquals(Rows[i], target[i]))
            {
                continue;
            }

            // 並びが変わった行は、一度外して入れ直す (行を動かす通知は、表が扱わないことがあるため、使わない)
            var existing = Rows.IndexOf(target[i]);
            if (existing >= 0)
            {
                Rows.RemoveAt(existing);
            }
            Rows.Insert(i, target[i]);
        }
        while (Rows.Count > target.Count)
        {
            Rows.RemoveAt(Rows.Count - 1);
        }

        foreach (var key in _rowByKey.Keys.Where(key => !alive.Contains(key)).ToList())
        {
            _rowByKey.Remove(key);
        }
        IsEmpty = Rows.Count == 0;
        EmptyMessage = !IsEmpty || _service.HasVisibleProjects ? "" : _masterEmpty ? "「案件の管理」から、案件を追加してください" : "表示する案件を選んでください";
        if (SelectedRow is not null && !Rows.Contains(SelectedRow))
        {
            SelectedRow = null;
        }
        RowsRebuilt?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>移動先の選択肢に、グループを木の順に足す</summary>
    /// <param name="nodes">並べる行</param>
    /// <param name="projectId">案件の番号</param>
    /// <param name="excluded">選択肢に出さない行 (移す行。その中も出さない)</param>
    /// <param name="level">字下げの段</param>
    /// <param name="destinations">足し先</param>
    private static void AddDestinations(IReadOnlyList<WorkNode> nodes, int projectId, WorkKey excluded, int level, List<MoveDestination> destinations)
    {
        foreach (var node in nodes.Where(n => n.Key.Kind == WorkRowKind.Group && n.Key != excluded))
        {
            destinations.Add(new MoveDestination(projectId, node.Key.Id, node.Name, level));
            AddDestinations(node.Children, projectId, excluded, level + 1, destinations);
        }
    }

    /// <summary>入力された文字を、日付にする</summary>
    /// <param name="text">入力された文字 (空は、日付なし)</param>
    /// <param name="date">日付。空なら null</param>
    /// <returns>読めたとき (空も含む) true</returns>
    private static bool TryParseDate(string text, out DateOnly? date)
    {
        date = null;
        if (text.Length == 0)
        {
            return true;
        }
        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }
        return false;
    }

    /// <summary>入力された文字を、時間にする</summary>
    /// <param name="text">入力された文字 (空は、時間なし)</param>
    /// <param name="hours">時間。空なら null</param>
    /// <returns>読めたとき (空も含む) true</returns>
    private static bool TryParseHours(string text, out double? hours)
    {
        hours = null;
        if (text.Length == 0)
        {
            return true;
        }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) && double.IsFinite(parsed))
        {
            hours = parsed;
            return true;
        }
        return false;
    }
}
