using MmmSdk.Core.Components.Storage;

namespace MmmTool.WorkItems.Core;

/// <summary>
/// 作業リストを読み書きし、メモリに最新の内容を持つ。アプリ全体で 1 つ。
/// </summary>
/// <param name="repository">保存先</param>
/// <param name="time">現在の時刻を知るための時計</param>
/// <remarks>
/// 入力の確認 (名前・日付・時間の範囲)、並べ替え・移動の位置の計算を行う。保存に成功してから、メモリを書き換え、<see cref="Changed"/> を発火する。
/// 予測できる入力の誤りは、例外ではなく、画面に出せるメッセージ (戻り値の文字列)で返す。保存の失敗は <see cref="DataFileException"/>。
/// 操作は 1 つずつ順に行う (同時に呼ばれても、順番に待つ)。
/// </remarks>
public sealed class WorkItemService(IWorkItemRepository repository, TimeProvider time)
{
    /// <summary>操作を順番に行うためのロック</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>読み込みの結果 (失敗したか・壊れたファイルを退避したか)</summary>
    private readonly LoadStatus _status = new();

    /// <summary>行の一覧 (論理削除済みは含まない)</summary>
    private List<WorkItem> _items = [];

    /// <summary>日ごとの記録</summary>
    private List<WorkRecord> _records = [];

    /// <summary>進捗度のラベル (値 → ラベル)</summary>
    private Dictionary<int, string> _labels = [];

    /// <summary>内容が変わった (読み込み・追加・更新・削除・記録・ラベルの保存)</summary>
    public event EventHandler? Changed;

    /// <summary>最後の読み込みに失敗したときのメッセージ。成功したら null</summary>
    public string? LoadError => _status.LoadError;

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ</summary>
    public string? RecoveryMessage => _status.RecoveryMessage;

    /// <summary>一度でも読み込みに成功したか</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>進捗度のラベルを、画面から編集できるか</summary>
    public bool CanEditProgressLabels => repository.CanEditProgressLabels;

    /// <summary>行の一覧 (並びは保存した順。木構造は <see cref="BuildTree"/> で作る)</summary>
    public IReadOnlyList<WorkItem> Items => _items;

    /// <summary>進捗度のラベル (値 → ラベル。ラベルが付いた値だけ)</summary>
    public IReadOnlyDictionary<int, string> ProgressLabels => _labels;

    /// <summary>今日の日付</summary>
    public DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>保存先から全データを読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <exception cref="DataFileException">読み込みに失敗した (<see cref="LoadError"/> にも残す)。</exception>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (snapshot, recoveryMessage) = await repository.LoadAsync(cancellationToken);
            _items = [.. snapshot.Items];
            _records = [.. snapshot.Records];
            _labels = new Dictionary<int, string>(snapshot.ProgressLabels);
            IsLoaded = true;
            _status.Succeeded(recoveryMessage, keepPreviousRecoveryMessage: true);
        }
        catch (DataFileException ex)
        {
            _status.Failed(ex);
            throw;
        }
        finally
        {
            _lock.Release();
        }
        OnChanged();
    }

    /// <summary>木構造と集計値を作る</summary>
    /// <param name="date">入力日</param>
    /// <returns>最上位の行の並び</returns>
    public IReadOnlyList<WorkItemNode> BuildTree(DateOnly date) => WorkItemTree.Build(_items, _records, date);

    /// <summary>ある日の、作業の記録を返す</summary>
    /// <param name="workItemId">作業の番号</param>
    /// <param name="date">日付</param>
    /// <returns>記録。無ければ null</returns>
    public WorkRecord? GetRecord(int workItemId, DateOnly date)
        => _records.FirstOrDefault(r => r.WorkItemId == workItemId && r.Date == date);

    /// <summary>行を追加する</summary>
    /// <param name="kind">追加する行の種類</param>
    /// <param name="parentId">追加先の親の番号 (0 は最上位)</param>
    /// <param name="afterItemId">この行の直後に置く (兄弟の番号)。0 なら末尾</param>
    /// <returns>追加した行</returns>
    /// <remarks>名前は必須なので、初期値は「新しい案件」「新しいグループ」「新しい作業」にする (空の名前を作らない)。作業の開始日の初期値は今日 (期限日は空)。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<WorkItem> AddAsync(WorkItemKind kind, int parentId, int afterItemId = 0)
    {
        await _lock.WaitAsync();
        try
        {
            var siblings = SiblingsOf(parentId);
            var index = afterItemId == 0 ? siblings.Count : siblings.FindIndex(s => s.Id == afterItemId) + 1;
            if (index <= 0)
            {
                index = siblings.Count;
            }

            var newItem = new WorkItem
            {
                ParentId = parentId,
                Kind = kind,
                SortOrder = index,
                Name = kind == WorkItemKind.Work ? "新しい作業" : parentId == 0 ? "新しい案件" : "新しいグループ",
                StartDate = kind == WorkItemKind.Work ? DateOnly.FromDateTime(DateTime.Today) : null,
            };
            var positions = new List<WorkItemPosition>();
            for (var i = index; i < siblings.Count; i++)
            {
                if (siblings[i].SortOrder != i + 1)
                {
                    positions.Add(new WorkItemPosition(siblings[i].Id, parentId, i + 1));
                }
            }

            var saved = await repository.AddAsync(newItem, positions);
            ApplyPositions(positions);
            _items.Add(saved);
            OnChanged();
            return saved;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>行の項目を更新する</summary>
    /// <param name="updated">更新後の行 (番号で、今の行を探す)</param>
    /// <returns>入力の誤りのメッセージ。保存したときと、変わっていないときは null</returns>
    /// <remarks>
    /// 親・種類・並びは、ここでは変えない。グループは、名前と備考だけを変える。
    /// 日付の逆転・時間の範囲外などは、保存せずにメッセージで返す。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> UpdateAsync(WorkItem updated)
    {
        await _lock.WaitAsync();
        try
        {
            var index = _items.FindIndex(i => i.Id == updated.Id);
            if (index < 0)
            {
                return "この行は、すでに削除されています。";
            }

            var current = _items[index];
            var candidate = current.Kind == WorkItemKind.Group
                ? current with { Name = updated.Name.Trim(), Note = updated.Note }
                : current with
                {
                    Name = updated.Name.Trim(),
                    Note = updated.Note,
                    Priority = updated.Priority,
                    StartDate = updated.StartDate,
                    DueDate = updated.DueDate,
                    Status = updated.Status,
                    Progress = updated.Progress,
                    PlannedHours = updated.PlannedHours is { } hours ? Math.Round(hours, 2) : null,
                };

            var error = Validate(candidate);
            if (error is not null)
            {
                return error;
            }
            if (candidate == current)
            {
                return null;
            }

            await repository.UpdateAsync(candidate);
            _items[index] = candidate;
            OnChanged();
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>行を、兄弟の中で上または下へ動かす</summary>
    /// <param name="id">行の番号</param>
    /// <param name="delta">動かす数 (-1 で上、1 で下)</param>
    /// <returns>動かしたら true (先頭・末尾で動かせなければ false)</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<bool> ShiftAsync(int id, int delta)
    {
        await _lock.WaitAsync();
        try
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item is null)
            {
                return false;
            }

            var siblings = SiblingsOf(item.ParentId);
            var from = siblings.FindIndex(s => s.Id == id);
            var to = from + delta;
            if (from < 0 || to < 0 || to >= siblings.Count)
            {
                return false;
            }

            (siblings[from], siblings[to]) = (siblings[to], siblings[from]);
            var positions = new List<WorkItemPosition>();
            for (var i = 0; i < siblings.Count; i++)
            {
                if (siblings[i].SortOrder != i)
                {
                    positions.Add(new WorkItemPosition(siblings[i].Id, item.ParentId, i));
                }
            }

            await repository.UpdatePositionsAsync(positions);
            ApplyPositions(positions);
            OnChanged();
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>行 (グループなら中身ごと)を、別のグループの下か最上位へ移し、移動先の末尾に置く</summary>
    /// <param name="id">移す行の番号</param>
    /// <param name="newParentId">移動先の親の番号 (0 は最上位)</param>
    /// <returns>入力の誤りのメッセージ (自分自身・自分の中へは移せない)。移したら null</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> MoveAsync(int id, int newParentId)
    {
        await _lock.WaitAsync();
        try
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item is null)
            {
                return "この行は、すでに削除されています。";
            }
            if (newParentId != 0 && _items.FirstOrDefault(i => i.Id == newParentId) is not { Kind: WorkItemKind.Group })
            {
                return "移動先は、グループを選んでください。";
            }
            if (newParentId == id || IsDescendant(newParentId, id))
            {
                return "自分自身と、自分の中のグループへは移せません。";
            }
            if (item.ParentId == newParentId)
            {
                return null;
            }

            var positions = new List<WorkItemPosition> { new(id, newParentId, SiblingsOf(newParentId).Count) };
            await repository.UpdatePositionsAsync(positions);
            ApplyPositions(positions);
            OnChanged();
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>削除すると消える件数を数える</summary>
    /// <param name="id">削除する行の番号</param>
    /// <returns>消えるグループ・作業・日ごとの記録の数</returns>
    public WorkItemDeleteImpact GetDeleteImpact(int id)
    {
        var ids = SelfAndDescendantIds(id);
        var targets = _items.Where(i => ids.Contains(i.Id)).ToList();
        return new WorkItemDeleteImpact(
            targets.Count(i => i.Kind == WorkItemKind.Group),
            targets.Count(i => i.Kind == WorkItemKind.Work),
            _records.Count(r => ids.Contains(r.WorkItemId)));
    }

    /// <summary>行を削除する (論理削除。グループは中身も一緒に)</summary>
    /// <param name="id">削除する行の番号</param>
    /// <returns>削除の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task DeleteAsync(int id)
    {
        await _lock.WaitAsync();
        try
        {
            var ids = SelfAndDescendantIds(id);
            if (ids.Count == 0)
            {
                return;
            }

            await repository.SetDeletedAsync([.. ids]);
            _items.RemoveAll(i => ids.Contains(i.Id));
            _records.RemoveAll(r => ids.Contains(r.WorkItemId));
            OnChanged();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>日ごとの記録 (実績・日の備考)を保存する</summary>
    /// <param name="workItemId">作業の番号</param>
    /// <param name="date">日付</param>
    /// <param name="hours">実績 (時間)</param>
    /// <param name="note">日の備考</param>
    /// <returns>入力の誤りのメッセージ。保存したときと、変わっていないときは null</returns>
    /// <remarks>実績が 0 で備考が空なら、その日の記録を消す。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> SetRecordAsync(int workItemId, DateOnly date, double hours, string note)
    {
        await _lock.WaitAsync();
        try
        {
            if (_items.FirstOrDefault(i => i.Id == workItemId) is not { Kind: WorkItemKind.Work })
            {
                return "この作業は、すでに削除されています。";
            }
            if (double.IsNaN(hours) || hours < 0 || hours > WorkItemLimits.DailyHoursMax)
            {
                return $"実績は 0 以上 {WorkItemLimits.DailyHoursMax:0} 以下で入力してください。";
            }
            if (note.Length > WorkItemLimits.NoteMaxLength)
            {
                return $"備考は {WorkItemLimits.NoteMaxLength} 文字までです。";
            }

            var record = new WorkRecord { WorkItemId = workItemId, Date = date, Hours = Math.Round(hours, 2), Note = note };
            var existing = GetRecord(workItemId, date);
            if (existing == record || (existing is null && record.IsEmpty))
            {
                return null;
            }

            await repository.SetRecordAsync(record);
            _records.RemoveAll(r => r.WorkItemId == workItemId && r.Date == date);
            if (!record.IsEmpty)
            {
                _records.Add(record);
            }
            OnChanged();
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>進捗度のラベルを保存する</summary>
    /// <param name="labels">値ごとのラベル (空は、ラベルなし)</param>
    /// <returns>入力の誤りのメッセージ。保存したら null</returns>
    /// <exception cref="InvalidOperationException">保存先が、画面からの編集を認めていない (<see cref="CanEditProgressLabels"/>)。</exception>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> SaveProgressLabelsAsync(IReadOnlyDictionary<int, string> labels)
    {
        var rows = WorkProgress.Values
            .Select(v => new WorkProgressLabel { Value = v, Label = labels.GetValueOrDefault(v, "").Trim() })
            .ToList();
        if (rows.Any(r => r.Label.Length > WorkProgress.LabelMaxLength))
        {
            return $"ラベルは {WorkProgress.LabelMaxLength} 文字までです。";
        }

        await _lock.WaitAsync();
        try
        {
            await repository.SaveProgressLabelsAsync(rows);
            _labels = rows.Where(r => r.Label.Length > 0).ToDictionary(r => r.Value, r => r.Label);
            OnChanged();
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>行が、あるグループの下 (段数を問わない)にあるか</summary>
    /// <param name="itemId">調べる行の番号</param>
    /// <param name="ancestorId">祖先かどうかを調べるグループの番号</param>
    /// <returns>下にあれば true</returns>
    public bool IsDescendant(int itemId, int ancestorId)
    {
        var visited = new HashSet<int>();
        var current = itemId;
        while (current != 0 && visited.Add(current))
        {
            var item = _items.FirstOrDefault(i => i.Id == current);
            if (item is null)
            {
                return false;
            }
            if (item.ParentId == ancestorId)
            {
                return true;
            }
            current = item.ParentId;
        }
        return false;
    }

    /// <summary>入力の誤りを調べる</summary>
    /// <param name="item">調べる行</param>
    /// <returns>画面に出せるメッセージ。正しければ null</returns>
    private static string? Validate(WorkItem item)
    {
        if (item.Name.Length == 0)
        {
            return "名前を入力してください。";
        }
        if (item.Name.Length > WorkItemLimits.NameMaxLength)
        {
            return $"名前は {WorkItemLimits.NameMaxLength} 文字までです。";
        }
        if (item.Note.Length > WorkItemLimits.NoteMaxLength)
        {
            return $"備考は {WorkItemLimits.NoteMaxLength} 文字までです。";
        }
        if (item.StartDate is { } start && item.DueDate is { } due && start > due)
        {
            return "開始日が期限日より後になっています。";
        }
        if (item.PlannedHours is { } planned && (double.IsNaN(planned) || planned < 0 || planned > WorkItemLimits.PlannedHoursMax))
        {
            return $"予定時間は 0 以上 {WorkItemLimits.PlannedHoursMax:0} 以下で入力してください。";
        }
        if (!WorkProgress.IsValid(item.Progress))
        {
            return "進捗度は、0 から 100 の 10 刻みで選んでください。";
        }
        return null;
    }

    /// <summary>同じ親の兄弟を、並び順に返す</summary>
    /// <param name="parentId">親の番号</param>
    /// <returns>兄弟の一覧 (並び順。呼ぶ側が並べ替えてよい新しい一覧)</returns>
    private List<WorkItem> SiblingsOf(int parentId)
        => [.. _items.Where(i => i.ParentId == parentId).OrderBy(i => i.SortOrder).ThenBy(i => i.Id)];

    /// <summary>位置の変更を、メモリの行に反映する</summary>
    /// <param name="positions">反映する位置</param>
    private void ApplyPositions(IReadOnlyList<WorkItemPosition> positions)
    {
        foreach (var position in positions)
        {
            var index = _items.FindIndex(i => i.Id == position.Id);
            if (index >= 0)
            {
                _items[index] = _items[index] with { ParentId = position.ParentId, SortOrder = position.SortOrder };
            }
        }
    }

    /// <summary>行と、その下のすべての行の番号を返す</summary>
    /// <param name="id">行の番号</param>
    /// <returns>番号の集合 (行が無ければ空)</returns>
    private HashSet<int> SelfAndDescendantIds(int id)
    {
        var result = new HashSet<int>();
        if (_items.All(i => i.Id != id))
        {
            return result;
        }

        var pending = new Stack<int>([id]);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!result.Add(current))
            {
                continue;
            }
            foreach (var child in _items.Where(i => i.ParentId == current))
            {
                pending.Push(child.Id);
            }
        }
        return result;
    }

    /// <summary><see cref="Changed"/> を発火する</summary>
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
