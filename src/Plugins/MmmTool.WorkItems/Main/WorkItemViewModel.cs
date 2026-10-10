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

namespace MmmTool.WorkItems.Main;

/// <summary>
/// 作業リストのページ。木構造の表・入力日・表示列を扱う。
/// </summary>
/// <remarks>
/// 表のセルへの入力は、確定したとき (フォーカスが外れたとき・Enter)に、その場で保存する (<c>Commit～</c>)。入力が受け入れられなかったときは、
/// 理由を <see cref="Error"/> に出し、入力欄を保存されている値に戻す。グループの開閉・非表示の列は、この PC の設定 (設定ストア)に持つ。
/// </remarks>
public sealed partial class WorkItemViewModel : ObservableObject
{
    /// <summary>非表示の列を、設定ストアに持つキー (列のキーを、カンマでつないだ文字列)</summary>
    private const string HiddenColumnsKey = "WorkItems.HiddenColumns";

    /// <summary>閉じているグループを、設定ストアに持つキー (行の番号を、カンマでつないだ文字列)</summary>
    private const string CollapsedItemsKey = "WorkItems.CollapsedItems";

    /// <summary>日付として受け付ける書式</summary>
    private static readonly string[] DateFormats = ["yyyy/M/d", "yyyy-M-d", "yyyy.M.d", "yyyyMMdd", "M/d", "M-d"];

    /// <summary>作業リストの読み書き</summary>
    private readonly WorkItemService _service;

    /// <summary>設定ストア</summary>
    private readonly ISettingsStore _settings;

    /// <summary>作業リストのダイアログ</summary>
    private readonly IWorkItemDialogService _dialogs;

    /// <summary>確認ダイアログ</summary>
    private readonly IDialogService _confirm;

    /// <summary>番号から、表の行を引く (使い回す)</summary>
    private readonly Dictionary<int, WorkItemRow> _rowById = [];

    /// <summary>閉じているグループの番号</summary>
    private readonly HashSet<int> _collapsed;

    /// <summary>非表示の列のキー</summary>
    private readonly HashSet<string> _hiddenColumns;

    /// <summary>進捗度の選択肢 (ラベルが変わらない間は、同じ一覧を使い回す)</summary>
    private IReadOnlyList<string> _progressOptions = [];

    /// <summary>壊れたファイルを退避した知らせを、もう出したか (サービスが、アプリの実行中ずっと持つので、ページを開くたびには出さない)</summary>
    private bool _recoveryMessageShown;

    /// <summary>表の行 (見えている行を 1 つにならしたもの。木の順)</summary>
    public ObservableCollection<WorkItemRow> Rows { get; } = [];

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>入力日 (実績 (日)・備考 (日)の日。既定は今日)</summary>
    [ObservableProperty]
    public partial DateOnly InputDate { get; set; }

    /// <summary>行が 1 つも無いか (追加のしかたの案内を出す)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>選んだ行</summary>
    [ObservableProperty]
    public partial WorkItemRow? SelectedRow { get; set; }

    /// <summary>進捗度のラベルを、画面から編集できるか</summary>
    public bool CanEditProgressLabels => _service.CanEditProgressLabels;

    /// <summary>追加した行の名前を、入力できる状態にしてほしい</summary>
    public event EventHandler<WorkItemRow>? FocusNameRequested;

    /// <summary>表示する列が変わった (起動時の読み込みを除く)</summary>
    public event EventHandler? ColumnVisibilityChanged;

    /// <summary>行の一覧を作り直した (列の幅を内容に合わせ直すため)</summary>
    public event EventHandler? RowsRebuilt;

    /// <summary>ViewModel を作る</summary>
    /// <param name="service">作業リストの読み書き</param>
    /// <param name="settings">設定ストア</param>
    /// <param name="dialogs">作業リストのダイアログ</param>
    /// <param name="confirm">確認ダイアログ</param>
    public WorkItemViewModel(WorkItemService service, ISettingsStore settings, IWorkItemDialogService dialogs, IDialogService confirm)
    {
        _service = service;
        _settings = settings;
        _dialogs = dialogs;
        _confirm = confirm;

        _hiddenColumns = [.. settings.Get(HiddenColumnsKey, "").Split(',', StringSplitOptions.RemoveEmptyEntries)];
        _collapsed = [.. settings.Get(CollapsedItemsKey, "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var id) ? id : 0).Where(id => id != 0)];
        service.Changed += (_, _) => OnServiceChanged();

        // 値の変更で表を作り直す処理が動くので、上の準備のあとに設定する
        InputDate = service.Today;
    }

    /// <summary>ページを開いたときに、保存先から読み直して表を作る</summary>
    /// <returns>初期化の完了を表すタスク</returns>
    public async Task InitializeAsync()
    {
        Error.Clear();
        try
        {
            await _service.LoadAsync();
            if (!_recoveryMessageShown && _service.RecoveryMessage is { } recovery)
            {
                _recoveryMessageShown = true;
                Error.Show(recovery);
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        Rebuild();
    }

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

    /// <summary>入力日が変わったら、実績 (日)・備考 (日)を作り直す</summary>
    /// <param name="value">新しい入力日</param>
    partial void OnInputDateChanged(DateOnly value) => Rebuild();

    /// <summary>グループを開く・閉じる</summary>
    /// <param name="row">開閉するグループの行</param>
    /// <returns>切り替えの完了を表すタスク</returns>
    public async Task ToggleAsync(WorkItemRow row)
    {
        if (!_collapsed.Remove(row.Id))
        {
            _collapsed.Add(row.Id);
        }
        await SaveCollapsedAsync();
        Rebuild();
    }

    /// <summary>行を追加する</summary>
    /// <param name="kind">追加する行の種類</param>
    /// <param name="target">右クリックした行 (グループなら中に、作業なら同じ階層に追加する。null なら最上位の末尾)</param>
    /// <returns>追加の完了を表すタスク</returns>
    public async Task AddAsync(WorkItemKind kind, WorkItemRow? target)
    {
        var parentId = 0;
        var afterId = 0;
        if (target is { IsGroup: true })
        {
            parentId = target.Id;
            if (_collapsed.Remove(parentId))
            {
                await SaveCollapsedAsync();
            }
        }
        else if (target is not null && FindItem(target.Id) is { } item)
        {
            parentId = item.ParentId;
            afterId = item.Id;
        }

        try
        {
            var added = await _service.AddAsync(kind, parentId, afterId);
            Error.Clear();
            if (_rowById.TryGetValue(added.Id, out var row))
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
    /// <returns>動かせれば true</returns>
    public bool CanShift(WorkItemRow row, int delta)
    {
        if (FindItem(row.Id) is not { } item)
        {
            return false;
        }

        var siblings = _service.Items.Where(i => i.ParentId == item.ParentId).OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        var to = siblings.FindIndex(i => i.Id == item.Id) + delta;
        return to >= 0 && to < siblings.Count;
    }

    /// <summary>行を、兄弟の中で上または下へ動かす</summary>
    /// <param name="row">行</param>
    /// <param name="delta">動かす数 (-1 で上、1 で下)</param>
    /// <returns>動かしの完了を表すタスク</returns>
    public async Task ShiftAsync(WorkItemRow row, int delta)
    {
        try
        {
            await _service.ShiftAsync(row.Id, delta);
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
        var destinations = new List<MoveDestination> { new(0, "最上位", 0) };
        AddDestinations(_service.BuildTree(InputDate), row.Id, 1, destinations);
        if (await _dialogs.PickDestinationAsync(destinations) is not { } destinationId)
        {
            return;
        }

        try
        {
            var error = await _service.MoveAsync(row.Id, destinationId);
            if (error is not null)
            {
                Error.Show(error);
                return;
            }

            Error.Clear();
            if (destinationId != 0 && _collapsed.Remove(destinationId))
            {
                await SaveCollapsedAsync();
                Rebuild();
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>確認してから、行を削除する (論理削除。グループは中身も一緒に)</summary>
    /// <param name="row">削除する行</param>
    /// <returns>削除の完了を表すタスク</returns>
    public async Task DeleteAsync(WorkItemRow row)
    {
        var impact = _service.GetDeleteImpact(row.Id);
        var message = row.IsGroup
            ? $"{(row.Level == 0 ? "案件" : "グループ")}「{row.Name}」を、中身ごと削除します。\nグループ {impact.Groups} 件・作業 {impact.Works} 件・日ごとの記録 {impact.Records} 件が、削除されます。"
            : $"作業「{row.Name}」を削除します。\n日ごとの記録 {impact.Records} 件も、削除されます。";
        if (!await _confirm.ConfirmAsync("削除の確認", message, "削除", "キャンセル"))
        {
            return;
        }

        try
        {
            await _service.DeleteAsync(row.Id);
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>進捗度のラベルを編集する (ローカルの保存先だけ)</summary>
    /// <returns>編集の完了を表すタスク</returns>
    [RelayCommand]
    private async Task EditProgressLabelsAsync()
    {
        if (await _dialogs.EditProgressLabelsAsync(_service.ProgressLabels) is not { } labels)
        {
            return;
        }

        try
        {
            var error = await _service.SaveProgressLabelsAsync(labels);
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
                await UpdateItemAsync(row, item => item with { Name = text });
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
            if (text != row.DayNote)
            {
                await SetRecordAsync(row, _service.GetRecord(row.Id, InputDate)?.Hours ?? 0, text);
            }
        }
        else if (text != row.Note)
        {
            await UpdateItemAsync(row, item => item with { Note = text });
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
            case "Priority" when index < WorkItemNames.Priorities.Count && index != row.PriorityIndex:
                await UpdateItemAsync(row, item => item with { Priority = WorkItemNames.Priorities[index] });
                break;
            case "Status" when index < WorkItemNames.Statuses.Count && index != row.StatusIndex:
                await UpdateItemAsync(row, item => item with { Status = WorkItemNames.Statuses[index] });
                break;
            case "Progress" when index < WorkProgress.Values.Count && index != row.ProgressIndex:
                await UpdateItemAsync(row, item => item with { Progress = WorkProgress.Values[index] });
                break;
        }
    }

    /// <summary>行の項目を更新する</summary>
    /// <param name="row">行</param>
    /// <param name="change">今の行から、更新後の行を作る処理</param>
    /// <returns>更新の完了を表すタスク</returns>
    private async Task UpdateItemAsync(WorkItemRow row, Func<WorkItem, WorkItem> change)
    {
        if (FindItem(row.Id) is not { } item)
        {
            return;
        }

        try
        {
            var error = await _service.UpdateAsync(change(item));
            if (error is null)
            {
                Error.Clear();
            }
            else
            {
                Reject(row, error);
            }
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
            var error = await _service.SetRecordAsync(row.Id, InputDate, hours, note);
            if (error is null)
            {
                Error.Clear();
            }
            else
            {
                Reject(row, error);
            }
        }
        catch (DataFileException ex)
        {
            Reject(row, ex.Message);
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

    /// <summary>保存先の内容が変わったとき、表を作り直す</summary>
    private void OnServiceChanged() => Rebuild();

    /// <summary>木構造から、見えている行の一覧を作り直す</summary>
    /// <remarks>同じ行は使い回して値だけを書き換え、行の増減・並びの変更は、差分だけを一覧に反映する (入力欄の位置とフォーカスを保つため)。</remarks>
    private void Rebuild()
    {
        var labels = _service.ProgressLabels;
        var options = WorkProgress.Values.Select(v => WorkProgress.Format(v, labels)).ToList();
        if (!options.SequenceEqual(_progressOptions))
        {
            _progressOptions = options;
        }

        var target = new List<WorkItemRow>();
        var alive = new HashSet<int>();
        void Visit(WorkItemNode node, int level)
        {
            var item = node.Item;
            if (!_rowById.TryGetValue(item.Id, out var row))
            {
                row = new WorkItemRow(item.Id, item.Kind);
                _rowById[item.Id] = row;
            }

            var expanded = !_collapsed.Contains(item.Id);
            row.Update(node, level, expanded, item.Kind == WorkItemKind.Work ? _service.GetRecord(item.Id, InputDate) : null, _progressOptions, labels);
            alive.Add(item.Id);
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
                MarkAlive(node);
            }
        }

        void MarkAlive(WorkItemNode node)
        {
            foreach (var child in node.Children.SelectMany(c => c.SelfAndDescendants()))
            {
                alive.Add(child.Item.Id);
            }
        }

        foreach (var root in _service.BuildTree(InputDate))
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

        foreach (var id in _rowById.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _rowById.Remove(id);
        }
        IsEmpty = Rows.Count == 0;
        if (SelectedRow is not null && !Rows.Contains(SelectedRow))
        {
            SelectedRow = null;
        }
        RowsRebuilt?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>移動先の選択肢に、グループを木の順に足す</summary>
    /// <param name="nodes">並べる行</param>
    /// <param name="excludedId">選択肢に出さない行の番号 (移す行。その中も出さない)</param>
    /// <param name="level">字下げの段</param>
    /// <param name="destinations">足し先</param>
    private static void AddDestinations(IReadOnlyList<WorkItemNode> nodes, int excludedId, int level, List<MoveDestination> destinations)
    {
        foreach (var node in nodes.Where(n => n.Item.Kind == WorkItemKind.Group && n.Item.Id != excludedId))
        {
            destinations.Add(new MoveDestination(node.Item.Id, node.Item.Name, level));
            AddDestinations(node.Children, excludedId, level + 1, destinations);
        }
    }

    /// <summary>閉じているグループを、設定に残す</summary>
    /// <returns>保存の完了を表すタスク</returns>
    private Task SaveCollapsedAsync() => _settings.SetAsync(CollapsedItemsKey, string.Join(',', _collapsed));

    /// <summary>番号から、行 (保存するデータ)を探す</summary>
    /// <param name="id">行の番号</param>
    /// <returns>行。無ければ null</returns>
    private WorkItem? FindItem(int id) => _service.Items.FirstOrDefault(i => i.Id == id);

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
