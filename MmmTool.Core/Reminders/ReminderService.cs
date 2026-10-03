using MmmSdk.Core.Storage;

namespace MmmTool.Core.Reminders;

/// <summary>
/// リマインダー（本体・対応状態）を読み書きし、メモリにキャッシュする。アプリ全体で 1 つ。
/// </summary>
/// <param name="repository">リマインダーの保存先</param>
/// <remarks>
/// 最初のアクセスで 1 度だけ読み込む。読み書きはスレッドセーフ。取得で返すのはキャッシュの複製なので、書き換えても保存はされない（保存は <see cref="SaveAsync"/> で）。
/// 保存ファイルが無い・空・壊れているときは空の一覧として扱う（壊れていたファイルは退避して <see cref="RecoveryMessage"/> に残す）。
/// </remarks>
public sealed class ReminderService(IReminderRepository repository)
{
    /// <summary>読み書きを順番に行うためのロック</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>リマインダー本体の一覧（論理削除済みも含む）。読み込むまでは null</summary>
    private List<Reminder>? _reminders;

    /// <summary>対応状態の一覧。読み込むまでは null</summary>
    private List<ReminderState>? _states;

    /// <summary>読み込みの結果</summary>
    /// <remarks>読み込みに失敗していたら保存を止める（保存しようとすると例外）。</remarks>
    private readonly LoadStatus _status = new();

    /// <summary>本体・状態のどちらかが変わった</summary>
    /// <remarks>保存したスレッドから発火する（任意のスレッドになりうる）。UI スレッドへの切り替えは受け取る側で行う。</remarks>
    public event EventHandler? Changed;

    /// <summary>ファイルを読めなかったときのメッセージ。正常なら null。</summary>
    /// <remarks>ロック・権限などで読めなかったとき。元のファイルを上書きで消さないよう、このときは保存を止める（保存しようとすると例外）。</remarks>
    public string? LoadError => _status.LoadError;

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。通常は null。</summary>
    public string? RecoveryMessage => _status.RecoveryMessage;

    /// <summary>リマインダー本体の一覧を取得する</summary>
    /// <param name="includeDeleted">論理削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>リマインダー本体の一覧（複製）</returns>
    public async Task<IReadOnlyList<Reminder>> GetRemindersAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, _) = await EnsureLoadedAsync(cancellationToken);
            return [.. reminders.Where(reminder => includeDeleted || !reminder.IsDeleted).Select(reminder => reminder with { })];
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>ある日の対象（その日に発生するリマインダー）と、その日の対応状態を取得する</summary>
    /// <param name="now">基準にする日時（この日付を「今日」とする）</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>その日の対象（時刻 → 参照番号の順）。論理削除済みは含めない。別の日の状態は未対応として扱う</returns>
    /// <remarks>
    /// 画面（メイン画面）と通知（<see cref="ReminderMonitor"/>）が同じ内容を見るよう、「今日の状態」の組み立てはここだけで行う。
    /// 本体と状態を同じロックの中で読むので、途中で保存が入っても食い違わない。
    /// </remarks>
    public async Task<IReadOnlyList<ReminderTarget>> GetTargetsAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, states) = await EnsureLoadedAsync(cancellationToken);
            var today = DateOnly.FromDateTime(now);
            var todayValue = ReminderDates.ToDateValue(now);
            var statuses = states
                .Where(state => state.Date == todayValue)
                .GroupBy(state => state.BaseNo)
                .ToDictionary(group => group.Key, group => group.Last().Status);
            return [.. reminders
                .Where(reminder => !reminder.IsDeleted && ReminderDates.ToTime(reminder.Time) is not null && ReminderDates.OccursOn(reminder, today))
                .OrderBy(reminder => reminder.Time)
                .ThenBy(reminder => reminder.No)
                .Select(reminder => new ReminderTarget(reminder with { }, statuses.GetValueOrDefault(reminder.No)))];
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>リマインダー本体を保存する（追加・更新）</summary>
    /// <param name="reminder">保存するリマインダー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した内容（採番後）の複製</returns>
    /// <remarks>
    /// <see cref="Reminder.No"/> が 0 なら新規として、最大 + 1 で採番する。
    /// それ以外は同じ <see cref="Reminder.No"/> の既存分を更新する（論理削除済みなら削除フラグを解除する）。
    /// </remarks>
    /// <exception cref="ArgumentException">指定の番号のリマインダーが無い。</exception>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task<Reminder> SaveAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        Reminder saved;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, _) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            if (reminder.No == 0)
            {
                var no = reminders.Count == 0 ? 1 : reminders.Max(item => item.No) + 1;
                saved = reminder with { No = no };
                reminders.Add(saved);
            }
            else
            {
                var index = reminders.FindIndex(item => item.No == reminder.No);
                if (index < 0)
                {
                    throw new ArgumentException($"番号 {reminder.No} のリマインダーがありません。", nameof(reminder));
                }
                saved = reminder with { IsDeleted = false };
                reminders[index] = saved;
            }

            await repository.SaveRemindersAsync(reminders, cancellationToken);
            saved = saved with { };
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return saved;
    }

    /// <summary>リマインダー本体を論理削除する（削除フラグを立てる）</summary>
    /// <param name="no">削除するリマインダーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があれば true</returns>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public Task<bool> DeleteAsync(int no, CancellationToken cancellationToken = default)
        => UpdateRemindersAsync(reminders =>
        {
            var index = reminders.FindIndex(item => item.No == no);
            if (index < 0 || reminders[index].IsDeleted)
            {
                return index >= 0;
            }
            reminders[index] = reminders[index] with { IsDeleted = true };
            return true;
        }, cancellationToken);

    /// <summary>リマインダー本体を物理削除する（一覧から完全に取り除く）</summary>
    /// <param name="no">削除するリマインダーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があれば true</returns>
    /// <remarks>
    /// 対応状態（<see cref="ReminderState"/>）も一緒に消す。残すと、あとで同じ番号が採番されたとき（最大の番号を消した場合）に、
    /// 無関係な新しいリマインダーが、前の状態（完了など）を引き継いでしまうため。
    /// 状態のファイルを先に書く（途中で失敗しても、状態が残るだけの側に倒す。本体が残って状態だけ消えるのは、未対応に戻るだけで害が小さい）。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task<bool> PurgeAsync(int no, CancellationToken cancellationToken = default)
    {
        bool purged;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, states) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            purged = reminders.Exists(item => item.No == no);
            if (purged)
            {
                if (states.RemoveAll(state => state.BaseNo == no) > 0)
                {
                    await repository.SaveStatesAsync(states, cancellationToken);
                }
                reminders.RemoveAll(item => item.No == no);
                await repository.SaveRemindersAsync(reminders, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }

        if (purged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return purged;
    }

    /// <summary>対応状態の一覧を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対応状態の一覧（複製）</returns>
    public async Task<IReadOnlyList<ReminderState>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (_, states) = await EnsureLoadedAsync(cancellationToken);
            return [.. states.Select(state => state with { })];
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>リマインダー 1 件の対応状態を取得する</summary>
    /// <param name="baseNo">リマインダー本体の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>まだ無ければ null</returns>
    public async Task<ReminderState?> GetStateAsync(int baseNo, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (_, states) = await EnsureLoadedAsync(cancellationToken);
            return states.Find(state => state.BaseNo == baseNo) is { } found ? found with { } : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>リマインダー 1 件の対応状態を保存する</summary>
    /// <param name="baseNo">リマインダー本体の番号</param>
    /// <param name="date">対象日（yyyyMMdd の整数）</param>
    /// <param name="status">対応状態</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>同じ参照番号の状態は 1 件に保ち、あれば対象日・値を上書きする。無ければ採番して追加する。</remarks>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task SetStateAsync(int baseNo, int date, ReminderStatus status, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (_, states) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            var index = states.FindIndex(state => state.BaseNo == baseNo);
            if (index >= 0)
            {
                states[index] = states[index] with { Date = date, Status = status };
            }
            else
            {
                var seq = states.Count == 0 ? 1 : states.Max(state => state.Seq) + 1;
                states.Add(new ReminderState { Seq = seq, BaseNo = baseNo, Date = date, Status = status });
            }

            await repository.SaveStatesAsync(states, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>本体の一覧を書き換えて、変わったときだけ保存する</summary>
    /// <param name="change">一覧を書き換える処理。変わったら true を返す</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変わって保存したとき true</returns>
    private async Task<bool> UpdateRemindersAsync(Func<List<Reminder>, bool> change, CancellationToken cancellationToken)
    {
        bool changed;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, _) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            changed = change(reminders);
            if (changed)
            {
                await repository.SaveRemindersAsync(reminders, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return changed;
    }

    /// <summary>未読み込みなら読み込む。呼ぶ側は <c>_lock</c> を取っておくこと</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>キャッシュしているリマインダー本体と対応状態の一覧</returns>
    /// <remarks>ファイルを読めなかったときは空の一覧として扱い、<see cref="LoadError"/> に残す。</remarks>
    private async Task<(List<Reminder> Reminders, List<ReminderState> States)> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_reminders is not null && _states is not null)
        {
            return (_reminders, _states);
        }

        List<DataFileException> failures = [];
        List<string> recoveries = [];

        try
        {
            var result = await repository.LoadRemindersAsync(cancellationToken);
            _reminders = result.Value;
            if (result.RecoveryMessage is not null) recoveries.Add(result.RecoveryMessage);
        }
        catch (DataFileException ex)
        {
            _reminders = [];
            failures.Add(ex);
        }

        try
        {
            var result = await repository.LoadStatesAsync(cancellationToken);
            _states = result.Value;
            if (result.RecoveryMessage is not null) recoveries.Add(result.RecoveryMessage);
        }
        catch (DataFileException ex)
        {
            _states = [];
            failures.Add(ex);
        }

        // 手で編集した JSON の時刻が正しくないと、「常に発動済み」のように誤って動くため、通知・今日の対象から外す。
        // 本体は消さない（外すのは判定のときだけ）ので、編集して直せる
        var invalidTimeNos = _reminders.Where(reminder => !reminder.IsDeleted && ReminderDates.ToTime(reminder.Time) is null)
            .Select(reminder => reminder.No)
            .ToList();
        if (invalidTimeNos.Count > 0)
        {
            recoveries.Add($"時刻が正しくないリマインダーがあります（番号 {string.Join("、", invalidTimeNos)}）。通知されません。編集して直してください。");
        }

        _status.Record(failures, recoveries);
        return (_reminders, _states);
    }

    /// <summary>読み込みに失敗していたら、保存させずに例外にする</summary>
    /// <exception cref="DataFileException">読み込みに失敗している。</exception>
    private void ThrowIfLoadFailed()
        => _status.ThrowIfSaveBlocked("ファイルを読めなかったため、元のデータを消さないよう保存を止めています。アプリを再起動してください。");
}
