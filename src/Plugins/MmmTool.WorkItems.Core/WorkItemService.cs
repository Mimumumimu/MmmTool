using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmTool.CodeMaster.Core;
using MmmSdk.Core.Utilities;

namespace MmmTool.WorkItems.Core;

/// <summary>
/// 作業管理を読み書きし、選んだ案件の最新の内容をメモリに持つ。アプリ全体で 1 つ。
/// </summary>
/// <param name="repository">保存先</param>
/// <param name="policy">更新してよいかの判定</param>
/// <param name="time">現在の時刻を知るための時計</param>
/// <param name="settings">この PC の設定 (選んだ案件の番号を残す)</param>
/// <remarks>
/// 入力の確認 (名前・日付・時間の範囲)、並べ替え・移動の位置の計算、他の人の更新の反映 (差分の取得)を行う。保存に成功してから、メモリを書き換え、<see cref="Changed"/> を発火する。
/// 予測できる入力の誤りは、例外ではなく、画面に出せるメッセージ (戻り値の文字列)で返す。保存の失敗は <see cref="DataFileException"/>。
/// 操作は 1 つずつ順に行う (同時に呼ばれても、順番に待つ。差分の取得も同じロックの中で行う)。
/// <see cref="StartPolling"/> は UI スレッドから呼ぶ。待ちの続きが UI スレッドに戻るので、<see cref="Changed"/> も UI スレッドから発火する。
/// </remarks>
public sealed class WorkItemService(IWorkItemRepository repository, IWorkItemEditPolicy policy, TimeProvider time, ISettingsStore settings)
{
    /// <summary>他の人の更新を取りに行く間隔</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    /// <summary>差分の取得で、基準の時刻から戻して読む長さ (確定前だった更新の取りこぼしを避ける)</summary>
    private static readonly TimeSpan PollOverlap = TimeSpan.FromSeconds(60);

    /// <summary>操作を順番に行うためのロック</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>読み込みの結果 (失敗したか・壊れたファイルを退避したか)</summary>
    private readonly LoadStatus _status = new();

    /// <summary>読み込んでいる案件</summary>
    private readonly Dictionary<int, WorkProject> _projects = [];

    /// <summary>読み込んでいるグループ</summary>
    private readonly Dictionary<int, WorkGroup> _groups = [];

    /// <summary>読み込んでいる作業</summary>
    private readonly Dictionary<int, WorkItem> _items = [];

    /// <summary>表示しない案件の番号 (この PC の設定に残す。ここに無い案件は、全部表示する)</summary>
    private readonly HashSet<int> _hidden = [];

    /// <summary>表示しない案件の番号を残す設定のキー</summary>
    private const string HiddenKey = WorkItemSettingKeys.HiddenProjects;

    /// <summary>作業管理のコードマスタ (状態・進捗度・優先度の選択肢)</summary>
    private CodeBook _codes = CodeBook.Empty;

    /// <summary>担当者にできる人 (削除済みも含む。名前を引くため)</summary>
    private IReadOnlyList<WorkAssignee> _assignees = [];

    /// <summary>記録を読んでいる日</summary>
    private DateOnly _day;

    /// <summary>その日の記録 (全員分)</summary>
    private List<WorkRecord> _dayRecords = [];

    /// <summary>スケジュールの実績 (表示する案件の、すべての日の記録。作業と日付で 1 件。実績が 0 より大きいものだけ)</summary>
    /// <remarks>スケジュールを開いている間だけ持つ (<see cref="_holdsScheduleRecords"/>)。表だけを開いている間は、読まず、差分も取らない。</remarks>
    private readonly Dictionary<(int WorkItemId, DateOnly Date), WorkRecord> _scheduleRecords = [];

    /// <summary>スケジュールの記録を持っているか (持っている間だけ、差分の取得で、すべての日の記録を読む)</summary>
    private bool _holdsScheduleRecords;

    /// <summary>定期的な取得を使っている画面の数 (作業管理の表とスケジュール。0 になったら止める)</summary>
    private int _pollUsers;

    /// <summary>その日の、自分の日報メモ (無ければ null)</summary>
    private WorkDayNote? _dayNote;

    /// <summary>差分の取得の基準の時刻 (保存先が返した、いちばん新しい更新日時。まだ無ければ null)</summary>
    private DateTimeOffset? _baseline;

    /// <summary>定期的な差分の取得を止めるためのトークン源 (動いていなければ null)</summary>
    private CancellationTokenSource? _pollCts;

    /// <summary>定期的な差分の取得の間隔を待つタイマー (手動で取得したとき、数え直す)</summary>
    private readonly RestartableTimer _pollTimer = new(PollInterval);

    /// <summary>内容が変わった (読み込み・追加・更新・削除・記録・他の人の更新の反映)</summary>
    public event EventHandler<WorkChangedEventArgs>? Changed;

    /// <summary>定期的な差分の取得に失敗した (画面に出せるメッセージ)</summary>
    public event EventHandler<string>? RefreshFailed;

    /// <summary>最後の読み込みに失敗したときのメッセージ。成功したら null</summary>
    public string? LoadError => _status.LoadError;

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ</summary>
    public string? RecoveryMessage => _status.RecoveryMessage;

    /// <summary>一度でも読み込みに成功したか</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>今のユーザーの番号</summary>
    public int UserId => repository.UserId;

    /// <summary>今日の日付</summary>
    public DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>記録を読んでいる日 (まだ読んでいなければ、今日)</summary>
    /// <remarks>表の入力日と同じ。別の画面が読み直すときに、この日を渡して、表の日をずらさないために使う。</remarks>
    public DateOnly Day => _day == default ? Today : _day;


    /// <summary>表示している案件が、1 つ以上あるか</summary>
    public bool HasVisibleProjects => _projects.Count > 0;

    /// <summary>読み込んでいる案件</summary>
    public IReadOnlyCollection<WorkProject> Projects => _projects.Values;

    /// <summary>読み込んでいるグループ</summary>
    public IReadOnlyCollection<WorkGroup> Groups => _groups.Values;

    /// <summary>読み込んでいる作業</summary>
    public IReadOnlyCollection<WorkItem> Items => _items.Values;

    /// <summary>作業管理のコードマスタ (状態・進捗度・優先度の選択肢)</summary>
    public CodeBook Codes => _codes;

    /// <summary>担当者を扱う保存先か (ローカルモードは false)</summary>
    public bool HasAssignees => repository.HasAssignees;

    /// <summary>担当者にできる人 (削除済みも含む。名前を引くため。表示名の順)</summary>
    public IReadOnlyList<WorkAssignee> Assignees => _assignees;

    /// <summary>担当者の表示名を引く</summary>
    /// <param name="userId">ユーザーの番号 (0 は未割り当て)</param>
    /// <returns>表示名。未割り当ては空文字、一覧に無い番号は「ユーザー {番号}」</returns>
    public string AssigneeName(int userId)
        => userId == 0 ? "" : _assignees.FirstOrDefault(a => a.UserId == userId)?.Name ?? $"ユーザー {userId}";

    /// <summary>選んだ案件の中身と、ある日の記録を、保存先から読み込む</summary>
    /// <param name="day">記録を読む日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <exception cref="DataFileException">読み込みに失敗した (<see cref="LoadError"/> にも残す)。</exception>
    public async Task LoadAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var hidden = ReadHidden();
            var data = await repository.LoadVisibleAsync(hidden, cancellationToken);
            var records = await repository.LoadDayAsync(day, hidden, exclude: true, cancellationToken);
            var dayNote = await repository.LoadDayNoteAsync(day, cancellationToken);
            _assignees = await repository.GetAssigneesAsync(cancellationToken);

            _projects.Clear();
            _groups.Clear();
            _items.Clear();
            Merge(data);
            _hidden.Clear();
            _hidden.UnionWith(hidden);
            _codes = new CodeBook(data.Codes);
            _day = day;
            _dayRecords = [.. records];
            _dayNote = dayNote;

            // 基準の時刻が変わるので、持っていたスケジュールの記録は捨てる (スケジュールが、あとから読み直す)
            _scheduleRecords.Clear();
            _holdsScheduleRecords = false;
            _baseline = new DateTimeOffset?[]
            {
                data.Projects.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
                data.Groups.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
                data.Items.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
                data.Codes.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
                records.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
            }.Max();
            IsLoaded = true;
            _status.Succeeded(repository.RecoveryMessage, keepPreviousRecoveryMessage: true);
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

    /// <summary>記録を読む日を変える (その日の記録だけを読み直す。案件とその中身は、日付に依存しないので読み直さない)</summary>
    /// <param name="day">記録を読む日</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    public async Task SetDayAsync(DateOnly day)
    {
        await _lock.WaitAsync();
        try
        {
            if (day == _day && IsLoaded)
            {
                return;
            }
            _dayRecords = [.. await repository.LoadDayAsync(day, _hidden, exclude: true)];
            _dayNote = await repository.LoadDayNoteAsync(day);
            _day = day;
        }
        finally
        {
            _lock.Release();
        }
        OnChanged();
    }

    /// <summary>他の人の更新を取り込む定期的な取得を始める</summary>
    /// <remarks>
    /// UI スレッドから呼ぶ。使う画面の数を数え (作業管理の表とスケジュール)、すでに動いていれば、数を増やすだけ。
    /// 画面の切り替えで、新しい画面の開始が、前の画面の停止より先でも後でも、取得が止まったままにならないため。
    /// </remarks>
    public void StartPolling()
    {
        _pollUsers++;
        if (_pollCts is not null)
        {
            return;
        }

        _pollCts = new CancellationTokenSource();
        PollAsync(_pollCts.Token).Forget();
    }

    /// <summary>定期的な取得を、この画面の分だけ止める (使う画面がなくなったら、取得を止める)</summary>
    public void StopPolling()
    {
        if (_pollUsers > 0)
        {
            _pollUsers--;
        }
        if (_pollUsers > 0)
        {
            return;
        }

        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    /// <summary>スケジュールの実績 (表示する案件の、すべての日の記録)を、保存先から読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>
    /// スケジュールを開くたびに、案件と中身の読み込み (<see cref="LoadAsync"/>)のあとに呼ぶ。読んだあとは、60 秒ごとの取得が、すべての日の記録の差分も取り込む。
    /// 離れるときは <see cref="ReleaseScheduleRecords"/> で捨てる。
    /// </remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    public async Task LoadScheduleRecordsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var records = await repository.LoadRecordsAsync(_hidden, exclude: true, cancellationToken);
            _scheduleRecords.Clear();
            foreach (var record in records)
            {
                _scheduleRecords[(record.WorkItemId, record.Date)] = record;
            }
            _holdsScheduleRecords = true;
        }
        finally
        {
            _lock.Release();
        }
        OnChanged();
    }

    /// <summary>スケジュールの実績を捨てる (スケジュールを離れたとき。以後、すべての日の記録の差分は読まない)</summary>
    public void ReleaseScheduleRecords()
    {
        _holdsScheduleRecords = false;
        _scheduleRecords.Clear();
    }

    /// <summary>基準の時刻より後に更新された行を読み、メモリに反映する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>取得の完了を表すタスク</returns>
    /// <remarks>返ってきた行は、種類と番号で突き合わせて反映するので、同じ行が何度届いても結果は変わらない。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var changed = false;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!IsLoaded)
            {
                return;
            }

            var since = _baseline is { } baseline ? baseline - PollOverlap : DateTimeOffset.MinValue;
            var changes = await repository.GetChangesAsync(since, _day, _holdsScheduleRecords, _hidden, cancellationToken);
            changed = await ApplyChangesAsync(changes, cancellationToken);
            if (changes.MaxUpdatedAt is { } max && (_baseline is null || max > _baseline))
            {
                _baseline = max;
            }
        }
        finally
        {
            _lock.Release();
        }

        if (changed)
        {
            OnChanged();
        }
    }

    /// <summary>他の人の更新を、今すぐ取り込む (利用者が更新ボタンを押したとき)</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>取り込みの完了を表すタスク</returns>
    /// <remarks>
    /// 定期的な取得 (<see cref="PollInterval"/>)と同じ取り込みを、間隔を待たずに行う。直後に自動の取り込みが重ならないよう、間隔は数え直す。
    /// 失敗は、定期的な取得と同じく <see cref="RefreshFailed"/> で知らせる。UI スレッドから呼ぶ。
    /// </remarks>
    public async Task RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        _pollTimer.Restart();
        await TryRefreshAsync(cancellationToken);
    }

    /// <summary>差分を取り込む (失敗は <see cref="RefreshFailed"/> で知らせる)</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>取り込みの完了を表すタスク</returns>
    private async Task TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshAsync(cancellationToken);
        }
        catch (DataFileException ex)
        {
            RefreshFailed?.Invoke(this, ex.Message);
        }
    }

    /// <summary>木構造と集計値を作る</summary>
    /// <param name="date">入力日</param>
    /// <param name="mineOnly">自分が担当の作業の行だけを出すか (グループ・案件の集計は、全員の作業から計算したまま)</param>
    /// <param name="completed">完了して日がたった作業の見え方 (隠しても、集計は変えない)</param>
    /// <returns>案件の行の並び</returns>
    /// <remarks>
    /// 絞り込みは表示だけで、読み込みのあとにメモリで行う。集計を変えないのは、案件・グループの進捗が「全体の進み具合」で、見る人によって数字が変わらないようにするため。
    /// 読み込みの時点で絞ると、他の人が担当を変えた作業が、差分の取得で届かず古いまま残る。
    /// </remarks>
    public IReadOnlyList<WorkNode> BuildTree(DateOnly date, bool mineOnly = false, CompletedDisplay completed = CompletedDisplay.Show)
        => BuildTree(_projects.Values, _groups.Values, _items.Values, _dayRecords, date, mineOnly, completed);

    /// <summary>作業管理の表に出す木を、1 つの案件だけ作る (案件を表示に加えたとき、その案件の行だけを差し込むため)</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="date">入力日</param>
    /// <param name="mineOnly">自分が担当の作業の行だけを出すか</param>
    /// <param name="completed">完了して日がたった作業の見え方</param>
    /// <returns>その案件の行。案件を読み込んでいなければ null</returns>
    /// <remarks>案件の中身は、ほかの案件に依存しないので、全体を作り直した内容のうち、この案件の部分と同じになる。</remarks>
    public WorkNode? BuildProjectTree(int projectId, DateOnly date, bool mineOnly, CompletedDisplay completed)
        => BuildTree(
            _projects.Values.Where(p => p.Id == projectId),
            _groups.Values.Where(g => g.ProjectId == projectId),
            _items.Values.Where(i => i.ProjectId == projectId),
            _dayRecords.Where(r => r.ProjectId == projectId),
            date,
            mineOnly,
            completed).FirstOrDefault();

    /// <summary>渡された案件・グループ・作業・記録から、作業管理の表に出す木を作る</summary>
    /// <param name="projects">案件</param>
    /// <param name="groups">グループ</param>
    /// <param name="items">作業</param>
    /// <param name="dayRecords">入力日の記録</param>
    /// <param name="date">入力日</param>
    /// <param name="mineOnly">自分が担当の作業の行だけを出すか</param>
    /// <param name="completed">完了して日がたった作業の見え方</param>
    /// <returns>案件の行の並び</returns>
    private IReadOnlyList<WorkNode> BuildTree(IEnumerable<WorkProject> projects, IEnumerable<WorkGroup> groups, IEnumerable<WorkItem> items, IEnumerable<WorkRecord> dayRecords, DateOnly date, bool mineOnly, CompletedDisplay completed)
    {
        var roots = WorkItemTree.Build(projects, groups, items, dayRecords, date, _codes);
        if (mineOnly && HasAssignees)
        {
            WorkItemTree.RemoveWorks(roots, item => item.AssigneeUserId == UserId);
        }
        if (completed != CompletedDisplay.Show)
        {
            WorkItemTree.RemoveFinished(roots, date, completed == CompletedDisplay.Hidden);
        }
        return roots;
    }

    /// <summary>スケジュールに出す内容を作る</summary>
    /// <param name="mineOnly">自分が担当の作業の行だけを出すか (案件・グループの棒と合計は、全員の作業から計算したまま)</param>
    /// <param name="completed">完了した作業の見え方 (作業管理の「過去の完了」とは別に選ぶ。今日完了した作業も隠す)</param>
    /// <returns>行の木・実績の棒・軸の範囲</returns>
    /// <remarks>
    /// 実績は、持っている記録 (<see cref="LoadScheduleRecordsAsync"/>)から作る。
    /// 絞り込みは表示だけで、読み込みのあとにメモリで行う (<see cref="BuildTree(DateOnly, bool, CompletedDisplay)"/>と同じ理由)。軸の範囲は、隠す前の内容から求める。
    /// </remarks>
    public ScheduleSnapshot BuildSchedule(bool mineOnly, CompletedDisplay completed)
        => BuildSchedule(mineOnly, completed, _projects.Values, _groups.Values, _items.Values, _scheduleRecords.Values);

    /// <summary>スケジュールに出す内容を、1 つの案件だけ作る (案件を表示に加えたとき、その案件の行だけを差し込むため)</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="mineOnly">自分が担当の作業の行だけを出すか (案件・グループの棒と合計は、全員の作業から計算したまま)</param>
    /// <param name="completed">完了した作業の見え方</param>
    /// <returns>その案件だけの行の木・実績の棒・軸の範囲 (軸の範囲は、この案件と今日だけから求めた値)。案件を読み込んでいなければ、行の木は空</returns>
    /// <remarks>案件の中身は、ほかの案件に依存しないので、全体を作り直した内容のうち、この案件の部分と同じになる。</remarks>
    public ScheduleSnapshot BuildScheduleOfProject(int projectId, bool mineOnly, CompletedDisplay completed)
        => BuildSchedule(
            mineOnly,
            completed,
            _projects.Values.Where(p => p.Id == projectId),
            _groups.Values.Where(g => g.ProjectId == projectId),
            _items.Values.Where(i => i.ProjectId == projectId),
            _scheduleRecords.Values.Where(r => r.ProjectId == projectId));

    /// <summary>渡された案件・グループ・作業・記録から、スケジュールに出す内容を作る</summary>
    /// <param name="mineOnly">自分が担当の作業の行だけを出すか</param>
    /// <param name="completed">完了した作業の見え方</param>
    /// <param name="projects">案件</param>
    /// <param name="groups">グループ</param>
    /// <param name="items">作業</param>
    /// <param name="records">実績の記録</param>
    /// <returns>行の木・実績の棒・軸の範囲</returns>
    private ScheduleSnapshot BuildSchedule(bool mineOnly, CompletedDisplay completed, IEnumerable<WorkProject> projects, IEnumerable<WorkGroup> groups, IEnumerable<WorkItem> items, IEnumerable<WorkRecord> records)
    {
        var today = Today;
        var roots = WorkItemTree.Build(projects, groups, items, [], today, _codes);
        var actuals = ScheduleActuals.Build(roots, records);
        var (start, end) = ScheduleSnapshot.Range(roots, actuals, today);
        if (mineOnly && HasAssignees)
        {
            WorkItemTree.RemoveWorks(roots, item => item.AssigneeUserId == UserId);
        }
        if (completed != CompletedDisplay.Show)
        {
            WorkItemTree.RemoveFinished(roots, completed == CompletedDisplay.Hidden);
        }
        return new ScheduleSnapshot(roots, actuals, start, end);
    }

    /// <summary>番号から、案件を探す</summary>
    /// <param name="id">案件の番号</param>
    /// <returns>案件。読み込んでいなければ null</returns>
    public WorkProject? GetProject(int id) => _projects.GetValueOrDefault(id);

    /// <summary>番号から、グループを探す</summary>
    /// <param name="id">グループの番号</param>
    /// <returns>グループ。読み込んでいなければ null</returns>
    public WorkGroup? GetGroup(int id) => _groups.GetValueOrDefault(id);

    /// <summary>番号から、作業を探す</summary>
    /// <param name="id">作業の番号</param>
    /// <returns>作業。読み込んでいなければ null</returns>
    public WorkItem? GetItem(int id) => _items.GetValueOrDefault(id);

    /// <summary>ある日の記録を返す (作業ごと・日ごとに 1 件)</summary>
    /// <param name="workItemId">作業の番号</param>
    /// <param name="date">日付</param>
    /// <returns>記録。無ければ null (読んでいる日以外は、常に null)</returns>
    public WorkRecord? GetRecord(int workItemId, DateOnly date)
        => date == _day ? _dayRecords.FirstOrDefault(r => r.WorkItemId == workItemId && !r.IsDeleted) : null;

    /// <summary>読んでいる日の、自分の日報メモを返す</summary>
    /// <returns>メモ (無ければ空文字)</returns>
    public string GetDayNote() => _dayNote?.Note ?? "";

    /// <summary>読んでいる日の、自分の日報メモを保存する</summary>
    /// <param name="date">日付 (読んでいる日)</param>
    /// <param name="note">メモ (空白だけのときは、空にする)</param>
    /// <returns>入力の誤りのメッセージ。保存したときと、変わっていないときは null</returns>
    /// <remarks>
    /// 空にしても、行は消さず、メモを空にする。作業報告では、空と、行が無いことを区別しない。持ち主は今のユーザーだけなので、更新の判定 (<see cref="IWorkItemEditPolicy"/>)は通さない。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> SetDayNoteAsync(DateOnly date, string note)
    {
        if (note.Length > WorkItemLimits.DayNoteMaxLength)
        {
            return $"日報メモは {WorkItemLimits.DayNoteMaxLength} 文字までです。";
        }

        var text = string.IsNullOrWhiteSpace(note) ? "" : note;
        await _lock.WaitAsync();
        try
        {
            if (date != _day || (_dayNote?.Note ?? "") == text)
            {
                return null;
            }
            _dayNote = await repository.SetDayNoteAsync(date, text);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>作業報告のために、担当が自分の作業と、その記録と、報告日の日報メモを読む</summary>
    /// <param name="from">実績の開始日 (記録を読み始める日)</param>
    /// <param name="reportDate">報告日 (予定の日。その日の記録も読む)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読んだ内容。テキストにするのは <see cref="WorkReportBuilder"/> (画面が、「時間を出す」の切り替えのたびに、読み直さずに作り直せる)</returns>
    /// <remarks>表示している案件・隠している行の設定には従わない。読んだ内容を、メモリの木とは別に扱うので、画面の状態を変えない。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    public Task<WorkReportData> LoadReportAsync(DateOnly from, DateOnly reportDate, CancellationToken cancellationToken = default)
        => repository.LoadReportAsync(from, reportDate, cancellationToken: cancellationToken);

    /// <summary>作業報告の実績の開始日の既定を返す</summary>
    /// <param name="reportDate">報告日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>報告日より前で、担当が自分の作業に工数が入った日か、作業を完了にした日の、直近の日。無ければ、報告日の前日</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    public async Task<DateOnly> GetDefaultReportFromAsync(DateOnly reportDate, CancellationToken cancellationToken = default)
        => await repository.GetLatestRecordedDateAsync(reportDate, cancellationToken) ?? reportDate.AddDays(-1);

    /// <summary>案件マスターを、名前で検索して読む</summary>
    /// <param name="query">検索語 (名前の一部。空なら絞らない)</param>
    /// <param name="activeOnly">完了済みの案件を除くか</param>
    /// <param name="selectedOnly">表示している案件だけを読むか (表示しない案件を除く)</param>
    /// <param name="selectedFirst">表示している案件を、先頭にまとめるか</param>
    /// <param name="offset">読み始める位置</param>
    /// <param name="count">読む件数の上限</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>案件の一覧</returns>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    public async Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, bool selectedOnly, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default)
    {
        // 表示しない案件の番号は、ロックの中で写しを取る (別の操作が書き換えている間に、数えない)
        int[] hidden;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            hidden = [.. _hidden];
        }
        finally
        {
            _lock.Release();
        }
        return await repository.SearchProjectsAsync(query.Trim(), activeOnly, selectedOnly, hidden, selectedFirst, offset, count, cancellationToken);
    }

    /// <summary>案件を追加する (表示しない案件ではないので、そのまま表示される)</summary>
    /// <param name="name">名前</param>
    /// <returns>追加した案件の識別か、入力の誤りのメッセージ</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<WorkAddResult> AddProjectAsync(string name)
    {
        name = name.Trim();
        if (ValidateName(name) is { } error)
        {
            return new WorkAddResult(null, error);
        }

        await _lock.WaitAsync();
        try
        {
            var project = await repository.AddProjectAsync(name);
            _projects[project.Id] = project;
            OnChangedLater();
            return new WorkAddResult(new WorkKey(WorkRowKind.Project, project.Id), null);
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>案件の名前・リンク・備考を更新する (読み込んでいない案件も、名前は直せる)</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="name">新しい名前 (変えないなら null)</param>
    /// <param name="link">新しいリンク (変えないなら null。空文字はリンクを消す)</param>
    /// <param name="note">新しい備考 (変えないなら null)</param>
    /// <returns>入力の誤りのメッセージ。保存したときと、変わっていないときは null</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> UpdateProjectAsync(int projectId, string? name, string? link, string? note)
    {
        name = name?.Trim();
        link = link?.Trim();
        var error = (name is null ? null : ValidateName(name)) ?? (link is null ? null : ValidateLink(link)) ?? (note is null ? null : ValidateNote(note)) ?? Deny(WorkRowKind.Project, projectId);
        if (error is not null)
        {
            return error;
        }

        await _lock.WaitAsync();
        try
        {
            var current = _projects.GetValueOrDefault(projectId);
            if (current is not null)
            {
                name = name == current.Name ? null : name;
                link = link == current.Link ? null : link;
                note = note == current.Note ? null : note;
            }
            if (name is null && link is null && note is null)
            {
                return null;
            }

            await repository.UpdateProjectAsync(projectId, name, link, note);
            if (current is not null)
            {
                _projects[projectId] = current with { Name = name ?? current.Name, Link = link ?? current.Link, Note = note ?? current.Note };
                OnChangedLater();
            }
            return null;
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>案件を削除する (論理削除。案件の行だけ。中のグループ・作業の行は残すので、案件の行を戻せば、中身も戻る)</summary>
    /// <param name="projectId">案件の番号</param>
    /// <returns>更新できないときのメッセージ。削除したら null</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> DeleteProjectAsync(int projectId)
    {
        if (Deny(WorkRowKind.Project, projectId) is { } denied)
        {
            return denied;
        }

        await _lock.WaitAsync();
        try
        {
            await repository.DeleteProjectAsync(projectId);
            RemoveProjectContent(projectId);
            if (_hidden.Remove(projectId))
            {
                await SaveHiddenAsync();
            }
            OnChangedLater();
            return null;
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>案件を、表示する・表示しない (表示しない案件は、この PC の設定に残す)</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="selected">選ぶか (false なら表示しない。表示しなくても、案件とその中身は消えない)</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task SetProjectSelectedAsync(int projectId, bool selected)
    {
        await _lock.WaitAsync();
        try
        {
            if (selected == !_hidden.Contains(projectId))
            {
                return;
            }

            if (selected)
            {
                // 読み込みに失敗したときに、非表示の印だけが外れないよう、読んでから外す
                Merge(await repository.LoadProjectsAsync([projectId]));
                _hidden.Remove(projectId);

                // 日の記録は、スケジュールが使わない (作業管理の表だけが使う。表は、開くたびに読み直す)。スケジュールを開いている間は、読まない。
                // 読むのは、この案件の分だけ (ほかの案件の記録は、すでに持っている。外したときに、この案件の分は捨ててある)
                if (!_holdsScheduleRecords)
                {
                    var records = await repository.LoadDayAsync(_day, [projectId], exclude: false);
                    _dayRecords.RemoveAll(r => r.ProjectId == projectId);
                    _dayRecords.AddRange(records);
                }
                if (_holdsScheduleRecords)
                {
                    foreach (var record in await repository.LoadRecordsAsync([projectId], exclude: false))
                    {
                        ApplyScheduleRecord(record);
                    }
                }
            }
            else
            {
                _hidden.Add(projectId);
                RemoveProjectContent(projectId);
                PruneScheduleRecords();
            }
            await SaveHiddenAsync();
            OnChangedLater(new WorkChangedEventArgs(selected ? WorkChangeKind.ProjectShown : WorkChangeKind.ProjectHidden, projectId));
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>グループまたは作業を追加する</summary>
    /// <param name="kind">追加する行の種類 (グループまたは作業)</param>
    /// <param name="projectId">追加先の案件の番号</param>
    /// <param name="parentGroupId">追加先の親のグループの番号 (0 は案件の直下)</param>
    /// <param name="after">この行の直後に置く (同じ親の下の兄弟)。null なら末尾</param>
    /// <returns>追加した行の識別か、追加しなかった理由</returns>
    /// <remarks>名前は必須なので、初期値は「新しいグループ」「新しい作業」にする (空の名前を作らない)。作業の開始日の初期値は今日 (期限日は空)。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<WorkAddResult> AddAsync(WorkRowKind kind, int projectId, int parentGroupId, WorkKey? after = null)
    {
        if (Deny(WorkRowKind.Project, projectId) is { } denied)
        {
            return new WorkAddResult(null, denied);
        }

        await _lock.WaitAsync();
        try
        {
            if (!_projects.ContainsKey(projectId) || (parentGroupId != 0 && _groups.GetValueOrDefault(parentGroupId)?.ProjectId != projectId))
            {
                return new WorkAddResult(null, "追加先は、すでに削除されています。");
            }

            var siblings = SiblingsOf(projectId, parentGroupId);
            var index = after is { } a ? siblings.FindIndex(s => s.Kind == a.Kind && s.Id == a.Id) + 1 : 0;
            if (index <= 0)
            {
                index = siblings.Count;
            }

            var positions = new List<WorkPosition>();
            for (var i = index; i < siblings.Count; i++)
            {
                positions.Add(new WorkPosition(siblings[i].Kind, siblings[i].Id, projectId, parentGroupId, i + 1));
            }

            WorkKey key;
            if (kind == WorkRowKind.Group)
            {
                var saved = await repository.AddGroupAsync(new WorkGroup { ProjectId = projectId, ParentGroupId = parentGroupId, SortOrder = index, Name = "新しいグループ" }, positions);
                _groups[saved.Id] = saved;
                key = new WorkKey(WorkRowKind.Group, saved.Id);
            }
            else
            {
                var saved = await repository.AddItemAsync(
                    new WorkItem { ProjectId = projectId, GroupId = parentGroupId, SortOrder = index, Name = "新しい作業", StartDate = Day, AssigneeUserId = UserId },
                    positions);
                _items[saved.Id] = saved;
                key = new WorkKey(WorkRowKind.Work, saved.Id);
            }
            ApplyPositions(positions);
            OnChangedLater();
            return new WorkAddResult(key, null);
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>グループの名前・備考を更新する</summary>
    /// <param name="updated">更新後の行 (番号で、今の行を探す)</param>
    /// <returns>入力の誤りのメッセージ。保存したときと、変わっていないときは null</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> UpdateGroupAsync(WorkGroup updated)
    {
        if (Deny(WorkRowKind.Group, updated.Id) is { } denied)
        {
            return denied;
        }

        await _lock.WaitAsync();
        try
        {
            if (!_groups.TryGetValue(updated.Id, out var current))
            {
                return RemovedMessage;
            }

            var candidate = current with { Name = updated.Name.Trim(), Link = updated.Link.Trim(), Note = updated.Note };
            if ((ValidateName(candidate.Name) ?? ValidateLink(candidate.Link) ?? ValidateNote(candidate.Note)) is { } error)
            {
                return error;
            }
            if (candidate == current)
            {
                return null;
            }

            await repository.UpdateGroupAsync(current, candidate);
            _groups[candidate.Id] = candidate;
            OnChangedLater();
            return null;
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>作業の項目を更新する</summary>
    /// <param name="updated">更新後の行 (番号で、今の行を探す)</param>
    /// <param name="inputDate">入力日 (状態が完了に変わったときの、完了日)</param>
    /// <returns>入力の誤りのメッセージ。保存したときと、変わっていないときは null</returns>
    /// <remarks>
    /// 位置 (案件・親・並び)と実績の累計は、ここでは変えない。日付の逆転・時間の範囲外などは、保存せずにメッセージで返す。
    /// 状態が完了に変わったときは完了日に入力日を入れ、完了から外れたときは消す (完了のままなら変えない)。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> UpdateItemAsync(WorkItem updated, DateOnly inputDate)
    {
        if (Deny(WorkRowKind.Work, updated.Id) is { } denied)
        {
            return denied;
        }

        await _lock.WaitAsync();
        try
        {
            if (!_items.TryGetValue(updated.Id, out var current))
            {
                return RemovedMessage;
            }

            var candidate = current with
            {
                Name = updated.Name.Trim(),
                Link = updated.Link.Trim(),
                Note = updated.Note,
                Priority = updated.Priority,
                StartDate = updated.StartDate,
                DueDate = updated.DueDate,
                Status = updated.Status,
                ProgressCodeNo = updated.ProgressCodeNo,
                AssigneeUserId = updated.AssigneeUserId,
                PlannedHours = updated.PlannedHours is { } hours && Math.Round(hours, 2) is var rounded && rounded != 0 ? rounded : null,
            };
            candidate = candidate with
            {
                CompletedDate = candidate.Status == WorkItemStatus.Closed
                    ? (current.Status == WorkItemStatus.Closed && current.CompletedDate is not null ? current.CompletedDate : inputDate)
                    : null,
            };

            var error = ValidateItem(candidate, _codes);
            if (error is not null)
            {
                return error;
            }
            if (candidate == current)
            {
                return null;
            }

            await repository.UpdateItemAsync(current, candidate);
            _items[candidate.Id] = candidate;
            OnChangedLater();
            return null;
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>行を、兄弟の中で上または下へ動かす</summary>
    /// <param name="key">行の識別 (グループまたは作業)</param>
    /// <param name="delta">動かす数 (-1 で上、1 で下)</param>
    /// <returns>動かしたら true (先頭・末尾で動かせなければ false)</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<bool> ShiftAsync(WorkKey key, int delta)
    {
        if (Deny(key.Kind, key.Id) is not null)
        {
            return false;
        }

        await _lock.WaitAsync();
        try
        {
            if (!TryGetParent(key, out var projectId, out var parentId))
            {
                return false;
            }

            var siblings = SiblingsOf(projectId, parentId);
            var from = siblings.FindIndex(s => s.Kind == key.Kind && s.Id == key.Id);
            var to = from + delta;
            if (from < 0 || to < 0 || to >= siblings.Count)
            {
                return false;
            }

            (siblings[from], siblings[to]) = (siblings[to], siblings[from]);
            var positions = new List<WorkPosition>();
            for (var i = 0; i < siblings.Count; i++)
            {
                if (siblings[i].SortOrder != i)
                {
                    positions.Add(new WorkPosition(siblings[i].Kind, siblings[i].Id, projectId, parentId, i));
                }
            }

            await repository.UpdatePositionsAsync(positions, []);
            ApplyPositions(positions);
            OnChangedLater();
            return true;
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>行 (グループなら中身ごと)を、別のグループの下か、案件の直下へ移し、移動先の末尾に置く</summary>
    /// <param name="key">移す行の識別 (グループまたは作業)</param>
    /// <param name="projectId">移動先の案件の番号</param>
    /// <param name="groupId">移動先のグループの番号 (0 は案件の直下)</param>
    /// <returns>入力の誤りのメッセージ (自分自身・自分の中へは移せない)。移したら null</returns>
    /// <remarks>別の案件へ移すときは、移す行 (グループなら中身も)の案件の番号と、その作業の記録の案件の番号も直す。移動元の案件の更新日時も更新する。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> MoveAsync(WorkKey key, int projectId, int groupId)
    {
        if (Deny(key.Kind, key.Id) is { } denied)
        {
            return denied;
        }

        await _lock.WaitAsync();
        try
        {
            if (!TryGetParent(key, out var oldProjectId, out var oldParentId))
            {
                return RemovedMessage;
            }
            if (!_projects.ContainsKey(projectId) || (groupId != 0 && _groups.GetValueOrDefault(groupId)?.ProjectId != projectId))
            {
                return "移動先は、すでに削除されています。";
            }
            if (key.Kind == WorkRowKind.Group && (groupId == key.Id || IsInsideGroup(groupId, key.Id)))
            {
                return "自分自身と、自分の中のグループへは移せません。";
            }
            if (oldProjectId == projectId && oldParentId == groupId)
            {
                return null;
            }

            var order = SiblingsOf(projectId, groupId).Count;
            var positions = new List<WorkPosition> { new(key.Kind, key.Id, projectId, groupId, order) };
            if (key.Kind == WorkRowKind.Group && oldProjectId != projectId)
            {
                // 案件をまたぐときは、中身の案件の番号も直す (親と並びは、そのまま)
                var (groupIds, itemIds) = Descendants(key.Id);
                positions.AddRange(groupIds.Where(id => id != key.Id).Select(id => _groups[id]).Select(g => new WorkPosition(WorkRowKind.Group, g.Id, projectId, g.ParentGroupId, g.SortOrder)));
                positions.AddRange(itemIds.Select(id => _items[id]).Select(i => new WorkPosition(WorkRowKind.Work, i.Id, projectId, i.GroupId, i.SortOrder)));
            }

            await repository.UpdatePositionsAsync(positions, oldProjectId == projectId ? [] : [oldProjectId, projectId]);
            ApplyPositions(positions);
            if (oldProjectId != projectId)
            {
                // 記録の案件の番号も直したので、読み込んでいる記録にも反映する
                var movedItems = positions.Where(p => p.Kind == WorkRowKind.Work).Select(p => p.Id).ToHashSet();
                for (var i = 0; i < _dayRecords.Count; i++)
                {
                    if (movedItems.Contains(_dayRecords[i].WorkItemId))
                    {
                        _dayRecords[i] = _dayRecords[i] with { ProjectId = projectId };
                    }
                }
            }
            OnChangedLater();
            return null;
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>グループまたは作業を削除する (論理削除。その行だけ。グループの中の行は残すが、親が無いので画面には出ない)</summary>
    /// <param name="key">行の識別 (グループまたは作業)</param>
    /// <returns>更新できないときのメッセージ。削除したら null</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> DeleteAsync(WorkKey key)
    {
        if (Deny(key.Kind, key.Id) is { } denied)
        {
            return denied;
        }

        await _lock.WaitAsync();
        try
        {
            if (key.Kind == WorkRowKind.Group ? !_groups.ContainsKey(key.Id) : !_items.ContainsKey(key.Id))
            {
                return null;
            }

            // 削除の印は、選んだ行だけに付ける (中の行には付けない)
            await repository.DeleteAsync(key.Kind == WorkRowKind.Group ? [key.Id] : [], key.Kind == WorkRowKind.Work ? [key.Id] : []);
            if (key.Kind == WorkRowKind.Group)
            {
                _groups.Remove(key.Id);
            }
            else
            {
                _items.Remove(key.Id);
                _dayRecords.RemoveAll(r => r.WorkItemId == key.Id);
            }
            PruneOrphans();
            OnChangedLater();
            return null;
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>日ごとの記録 (実績・日の備考)を保存する</summary>
    /// <param name="workItemId">作業の番号</param>
    /// <param name="date">日付</param>
    /// <param name="hours">実績 (時間)</param>
    /// <param name="note">日の備考</param>
    /// <returns>入力の誤りのメッセージ。保存したときと、変わっていないときは null</returns>
    /// <remarks>実績が 0 で備考が空なら、その日の記録を論理削除する。保存先が、作業の実績の累計も計算し直す。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task<string?> SetRecordAsync(int workItemId, DateOnly date, double hours, string note)
    {
        if (Deny(WorkRowKind.Work, workItemId) is { } denied)
        {
            return denied;
        }
        if (double.IsNaN(hours) || hours < 0 || hours > WorkItemLimits.DailyHoursMax)
        {
            return $"実績は 0 以上 {WorkItemLimits.DailyHoursMax:0} 以下で入力してください。";
        }
        if (ValidateNote(note) is { } noteError)
        {
            return noteError;
        }

        await _lock.WaitAsync();
        try
        {
            if (!_items.TryGetValue(workItemId, out var item))
            {
                return "この作業は、すでに削除されています。";
            }

            var record = new WorkRecord { ProjectId = item.ProjectId, WorkItemId = workItemId, Date = date, Hours = Math.Round(hours, 2), Note = note };
            var existing = GetRecord(workItemId, date);
            if (existing is not null ? existing.Hours == record.Hours && existing.Note == record.Note : record.IsEmpty)
            {
                return null;
            }

            var result = await repository.SetRecordAsync(record);
            if (date == _day)
            {
                _dayRecords.RemoveAll(r => r.WorkItemId == workItemId && r.Date == date);
                if (result.Record is { IsDeleted: false } saved)
                {
                    _dayRecords.Add(saved);
                }
            }
            if (_holdsScheduleRecords)
            {
                if (result.Record is { } savedRecord)
                {
                    ApplyScheduleRecord(savedRecord);
                }
                else
                {
                    _scheduleRecords.Remove((workItemId, date));
                }
            }
            _items[workItemId] = item with { ActualTotalHours = result.ActualTotalHours };
            OnChangedLater();
            return null;
        }
        finally
        {
            _lock.Release();
            FlushChanged();
        }
    }

    /// <summary>行を、兄弟の中で上または下へ動かせるか</summary>
    /// <param name="key">行の識別 (グループまたは作業)</param>
    /// <param name="delta">動かす数 (-1 で上、1 で下)</param>
    /// <returns>動かせれば true (先頭・末尾では false)</returns>
    public bool CanShift(WorkKey key, int delta)
    {
        if (!TryGetParent(key, out var projectId, out var parentId))
        {
            return false;
        }

        var siblings = SiblingsOf(projectId, parentId);
        var to = siblings.FindIndex(s => s.Kind == key.Kind && s.Id == key.Id) + delta;
        return to >= 0 && to < siblings.Count;
    }

    /// <summary>行が、あるグループの中 (段数を問わない)にあるグループか</summary>
    /// <param name="groupId">調べるグループの番号</param>
    /// <param name="ancestorGroupId">祖先かどうかを調べるグループの番号</param>
    /// <returns>中にあれば true</returns>
    public bool IsInsideGroup(int groupId, int ancestorGroupId)
    {
        var visited = new HashSet<int>();
        var current = groupId;
        while (current != 0 && visited.Add(current))
        {
            if (!_groups.TryGetValue(current, out var group))
            {
                return false;
            }
            if (group.ParentGroupId == ancestorGroupId)
            {
                return true;
            }
            current = group.ParentGroupId;
        }
        return false;
    }

    /// <summary>行を更新できない理由</summary>
    private const string DeniedMessage = "この行は、更新できません。";

    /// <summary>行が見つからないときのメッセージ</summary>
    private const string RemovedMessage = "この行は、すでに削除されています。";

    /// <summary>定期的な差分の取得を繰り返す</summary>
    /// <param name="cancellationToken">止めるためのトークン</param>
    /// <returns>繰り返しの完了を表すタスク</returns>
    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _pollTimer.WaitForNextTickAsync(cancellationToken))
            {
                await TryRefreshAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 止められた
        }
    }

    /// <summary>この PC の設定から、表示しない案件の番号を読む</summary>
    /// <returns>案件の番号 (重なりなし。ユーザーを特定していないときは空)</returns>
    /// <remarks>
    /// キーは <c>WorkItems.HiddenProjects</c>、値は案件の番号をカンマでつないだ文字列 (例: <c>1,5,7</c>)。無ければ、全部の案件を表示する。
    /// モード (ローカル・サーバー)の切り替えは想定せず、キーを分けない。切り替えたときは、この設定を消す (<c>IDatabaseModeChangeHandler</c>)。
    /// 削除された案件の番号が残っても、読み込みに影響しない (除く対象が、無いだけ)。
    /// </remarks>
    private List<int> ReadHidden()
    {
        if (UserId == 0)
        {
            return [];
        }

        return [.. settings.Get(HiddenKey, "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()];
    }

    /// <summary>表示しない案件の番号を、この PC の設定に残す</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>設定を保存できなくても、この起動の間は、メモリの内容で動く (設定の保存の失敗は、設定ストアが扱う)。</remarks>
    private async Task SaveHiddenAsync()
    {
        if (UserId != 0)
        {
            await settings.SetAsync(HiddenKey, string.Join(',', _hidden.Order()));
        }
    }

    /// <summary>差分の内容を、メモリに反映する</summary>
    /// <param name="changes">差分</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>メモリの内容が変わったら true</returns>
    private async Task<bool> ApplyChangesAsync(WorkChanges changes, CancellationToken cancellationToken)
    {
        var changed = false;

        // 案件の行が変わったとき (名前の変更・行が減った移動元・削除)は、その案件の中身を読み直す
        var reload = new HashSet<int>();
        foreach (var project in changes.Projects.Where(p => !_hidden.Contains(p.Id)))
        {
            if (project.IsDeleted)
            {
                RemoveProjectContent(project.Id);
                changed = true;
            }
            else if (!_projects.TryGetValue(project.Id, out var current) || current.UpdatedAt != project.UpdatedAt)
            {
                reload.Add(project.Id);
                changed = true;
            }
        }
        if (reload.Count > 0)
        {
            foreach (var id in reload)
            {
                RemoveProjectContent(id);
            }
            Merge(await repository.LoadProjectsAsync([.. reload], cancellationToken));
        }

        // 読み直した案件より後の変更は、読み直した内容に含まれるので、残りの案件の行だけ突き合わせる
        foreach (var group in changes.Groups.Where(g => !_hidden.Contains(g.ProjectId) && !reload.Contains(g.ProjectId)))
        {
            changed |= group.IsDeleted ? _groups.Remove(group.Id) : Upsert(_groups, group.Id, group);
        }
        foreach (var item in changes.Items.Where(i => !_hidden.Contains(i.ProjectId) && !reload.Contains(i.ProjectId)))
        {
            changed |= item.IsDeleted ? _items.Remove(item.Id) : Upsert(_items, item.Id, item);
        }
        changed |= PruneOrphans();
        if (_holdsScheduleRecords)
        {
            PruneScheduleRecords();
            foreach (var record in changes.Records.Where(r => !_hidden.Contains(r.ProjectId)))
            {
                changed |= ApplyScheduleRecord(record);
            }
        }
        foreach (var record in changes.Records.Where(r => !_hidden.Contains(r.ProjectId) && r.Date == _day))
        {
            var index = _dayRecords.FindIndex(r => r.Id == record.Id);
            if (record.IsDeleted)
            {
                if (index >= 0)
                {
                    _dayRecords.RemoveAt(index);
                    changed = true;
                }
            }
            else if (index < 0)
            {
                _dayRecords.Add(record);
                changed = true;
            }
            else if (_dayRecords[index] != record)
            {
                _dayRecords[index] = record;
                changed = true;
            }
        }

        var codes = _codes.With(changes.Codes.Where(c => _codes.Find(c.FeatureNo, c.CategoryNo, c.CodeNo) != c));
        if (!ReferenceEquals(codes, _codes))
        {
            _codes = codes;
            changed = true;
        }
        return changed;
    }

    /// <summary>届いた記録 (または、保存した記録)を、スケジュールの実績に反映する</summary>
    /// <param name="record">記録 (論理削除された行・実績が 0 の行は、実績から外す)</param>
    /// <returns>実績が変わったら true</returns>
    private bool ApplyScheduleRecord(WorkRecord record)
    {
        var key = (record.WorkItemId, record.Date);
        if (record.IsDeleted || record.Hours <= 0)
        {
            return _scheduleRecords.Remove(key);
        }

        if (_scheduleRecords.TryGetValue(key, out var current) && current == record)
        {
            return false;
        }
        _scheduleRecords[key] = record;
        return true;
    }

    /// <summary>作業が無くなった記録を、スケジュールの実績から外す (表示しなくなった案件・削除された作業の分)</summary>
    private void PruneScheduleRecords()
    {
        foreach (var key in _scheduleRecords.Keys.Where(k => !_items.ContainsKey(k.WorkItemId)).ToList())
        {
            _scheduleRecords.Remove(key);
        }
    }

    /// <summary>辞書に行を入れる (同じ内容なら何もしない)</summary>
    /// <typeparam name="T">行の型</typeparam>
    /// <param name="dictionary">入れ先</param>
    /// <param name="id">行の番号</param>
    /// <param name="value">行</param>
    /// <returns>内容が変わったら true</returns>
    private static bool Upsert<T>(Dictionary<int, T> dictionary, int id, T value) where T : class
    {
        if (dictionary.TryGetValue(id, out var current) && current.Equals(value))
        {
            return false;
        }
        dictionary[id] = value;
        return true;
    }

    /// <summary>読み込んだ案件の中身を、メモリに入れる</summary>
    /// <param name="data">読み込んだ内容</param>
    private void Merge(WorkLoadResult data)
    {
        foreach (var project in data.Projects)
        {
            _projects[project.Id] = project;
        }
        foreach (var group in data.Groups)
        {
            _groups[group.Id] = group;
        }
        foreach (var item in data.Items)
        {
            _items[item.Id] = item;
        }
        PruneOrphans();
    }

    /// <summary>案件の行が無い行・親のグループの行が無い行を、メモリから外す</summary>
    /// <returns>外した行があれば true</returns>
    /// <remarks>
    /// 案件やグループの削除は、その行だけに論理削除の印を付け、中の行には付けない。
    /// そのため、読み込んだ中に、削除された案件・グループの中の行が混ざる。親をたどれない行は、画面に出さない。
    /// </remarks>
    private bool PruneOrphans()
    {
        var removed = false;
        bool pruned;
        do
        {
            pruned = false;
            foreach (var group in _groups.Values.Where(g => !_projects.ContainsKey(g.ProjectId) || (g.ParentGroupId != 0 && !_groups.ContainsKey(g.ParentGroupId))).ToList())
            {
                _groups.Remove(group.Id);
                pruned = true;
            }
            removed |= pruned;
        }
        while (pruned);

        foreach (var item in _items.Values.Where(i => !_projects.ContainsKey(i.ProjectId) || (i.GroupId != 0 && !_groups.ContainsKey(i.GroupId))).ToList())
        {
            _items.Remove(item.Id);
            removed = true;
        }
        return removed;
    }

    /// <summary>案件の中身 (案件の行・グループ・作業・記録)を、メモリから外す</summary>
    /// <param name="projectId">案件の番号</param>
    private void RemoveProjectContent(int projectId)
    {
        _projects.Remove(projectId);
        foreach (var id in _groups.Values.Where(g => g.ProjectId == projectId).Select(g => g.Id).ToList())
        {
            _groups.Remove(id);
        }
        foreach (var id in _items.Values.Where(i => i.ProjectId == projectId).Select(i => i.Id).ToList())
        {
            _items.Remove(id);
        }
        _dayRecords.RemoveAll(r => r.ProjectId == projectId);
    }

    /// <summary>同じ親の兄弟 (グループと作業)を、並び順に返す</summary>
    /// <param name="projectId">案件の番号</param>
    /// <param name="parentGroupId">親のグループの番号 (0 は案件の直下)</param>
    /// <returns>兄弟の一覧 (並び順。呼ぶ側が書き換えてよい新しい一覧)</returns>
    private List<(WorkRowKind Kind, int Id, int SortOrder)> SiblingsOf(int projectId, int parentGroupId)
        => [.. _groups.Values.Where(g => g.ProjectId == projectId && g.ParentGroupId == parentGroupId).Select(g => (WorkRowKind.Group, g.Id, g.SortOrder))
            .Concat(_items.Values.Where(i => i.ProjectId == projectId && i.GroupId == parentGroupId).Select(i => (WorkRowKind.Work, i.Id, i.SortOrder)))
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Item1).ThenBy(s => s.Item2)];

    /// <summary>行の案件と親を調べる</summary>
    /// <param name="key">行の識別 (グループまたは作業)</param>
    /// <param name="projectId">案件の番号</param>
    /// <param name="parentId">親のグループの番号 (0 は案件の直下)</param>
    /// <returns>行が見つかったら true</returns>
    private bool TryGetParent(WorkKey key, out int projectId, out int parentId)
    {
        projectId = 0;
        parentId = 0;
        if (key.Kind == WorkRowKind.Group && _groups.TryGetValue(key.Id, out var group))
        {
            (projectId, parentId) = (group.ProjectId, group.ParentGroupId);
            return true;
        }
        if (key.Kind == WorkRowKind.Work && _items.TryGetValue(key.Id, out var item))
        {
            (projectId, parentId) = (item.ProjectId, item.GroupId);
            return true;
        }
        return false;
    }

    /// <summary>位置の変更を、メモリの行に反映する</summary>
    /// <param name="positions">反映する位置</param>
    private void ApplyPositions(IReadOnlyList<WorkPosition> positions)
    {
        foreach (var position in positions)
        {
            if (position.Kind == WorkRowKind.Group && _groups.TryGetValue(position.Id, out var group))
            {
                _groups[position.Id] = group with { ProjectId = position.ProjectId, ParentGroupId = position.ParentId, SortOrder = position.SortOrder };
            }
            else if (position.Kind == WorkRowKind.Work && _items.TryGetValue(position.Id, out var item))
            {
                _items[position.Id] = item with { ProjectId = position.ProjectId, GroupId = position.ParentId, SortOrder = position.SortOrder };
            }
        }
    }

    /// <summary>グループと、その下のすべてのグループ・作業の番号を返す</summary>
    /// <param name="groupId">グループの番号</param>
    /// <returns>グループの番号 (自分を含む)と作業の番号。グループが無ければ、どちらも空</returns>
    private (HashSet<int> GroupIds, HashSet<int> ItemIds) Descendants(int groupId)
    {
        var groupIds = new HashSet<int>();
        var itemIds = new HashSet<int>();
        if (!_groups.ContainsKey(groupId))
        {
            return (groupIds, itemIds);
        }

        var pending = new Stack<int>([groupId]);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!groupIds.Add(current))
            {
                continue;
            }
            foreach (var child in _groups.Values.Where(g => g.ParentGroupId == current))
            {
                pending.Push(child.Id);
            }
            foreach (var item in _items.Values.Where(i => i.GroupId == current))
            {
                itemIds.Add(item.Id);
            }
        }
        return (groupIds, itemIds);
    }

    /// <summary>更新してよくないときのメッセージを返す</summary>
    /// <param name="kind">行の種類</param>
    /// <param name="id">行の番号</param>
    /// <returns>更新できないときのメッセージ。更新してよければ null</returns>
    private string? Deny(WorkRowKind kind, int id) => policy.CanEdit(kind, id) ? null : DeniedMessage;

    /// <summary>名前の誤りを調べる</summary>
    /// <param name="name">名前 (前後の空白を除いたもの)</param>
    /// <returns>画面に出せるメッセージ。正しければ null</returns>
    private static string? ValidateName(string name)
    {
        if (name.Length == 0)
        {
            return "名前を入力してください。";
        }
        return name.Length > WorkItemLimits.NameMaxLength ? $"名前は {WorkItemLimits.NameMaxLength} 文字までです。" : null;
    }

    /// <summary>備考の誤りを調べる</summary>
    /// <param name="note">備考</param>
    /// <returns>画面に出せるメッセージ。正しければ null</returns>
    private static string? ValidateNote(string note)
        => note.Length > WorkItemLimits.NoteMaxLength ? $"備考は {WorkItemLimits.NoteMaxLength} 文字までです。" : null;

    /// <summary>リンクの誤りを調べる (長さだけ。開く種類は絞らない)</summary>
    /// <param name="link">リンク (前後の空白を除いたもの)</param>
    /// <returns>画面に出せるメッセージ。正しければ null</returns>
    private static string? ValidateLink(string link)
        => link.Length > WorkItemLimits.LinkMaxLength ? $"リンクは {WorkItemLimits.LinkMaxLength} 文字までです。" : null;

    /// <summary>作業の入力の誤りを調べる</summary>
    /// <param name="item">調べる作業</param>
    /// <param name="codes">コードブック</param>
    /// <returns>画面に出せるメッセージ。正しければ null</returns>
    private static string? ValidateItem(WorkItem item, CodeBook codes)
    {
        if ((ValidateName(item.Name) ?? ValidateLink(item.Link) ?? ValidateNote(item.Note)) is { } basic)
        {
            return basic;
        }
        if (item.StartDate is { } start && item.DueDate is { } due && start > due)
        {
            return "開始日が期限日より後になっています。";
        }
        if (item.PlannedHours is { } planned && (double.IsNaN(planned) || planned < 0 || planned > WorkItemLimits.PlannedHoursMax))
        {
            return $"予定時間は 0 以上 {WorkItemLimits.PlannedHoursMax:0} 以下で入力してください。";
        }
        if (codes.Find(WorkCodes.FeatureNo, WorkCodes.ProgressCategory, item.ProgressCodeNo) is null)
        {
            return "進捗度は、一覧から選んでください。";
        }
        return null;
    }

    /// <summary>ロックを持っている間に溜めた、変更の通知 (無ければ null)</summary>
    private WorkChangedEventArgs? _pendingChanged;

    /// <summary>変更の通知を、ロックを放したあとに出すよう予約する</summary>
    /// <param name="args">通知の内容 (省略すると、全体の変更)</param>
    /// <remarks>違う内容の通知が重なったときは、全体の変更にまとめる (どちらの変更も、全体を作り直せば反映できるため)。</remarks>
    private void OnChangedLater(WorkChangedEventArgs? args = null)
    {
        args ??= WorkChangedEventArgs.All;
        _pendingChanged = _pendingChanged is null || _pendingChanged == args ? args : WorkChangedEventArgs.All;
    }

    /// <summary>予約した変更の通知を出す</summary>
    private void FlushChanged()
    {
        if (_pendingChanged is { } args)
        {
            _pendingChanged = null;
            Changed?.Invoke(this, args);
        }
    }

    /// <summary><see cref="Changed"/> を、全体の変更として発火する</summary>
    private void OnChanged() => Changed?.Invoke(this, WorkChangedEventArgs.All);
}
