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

    /// <summary>読み込みに失敗したときの例外。正常なら null</summary>
    /// <remarks>あれば保存を止める。</remarks>
    private DataFileException? _loadException;

    /// <summary>本体・状態のどちらかが変わった</summary>
    /// <remarks>保存したスレッドから発火する（任意のスレッドになりうる）。UI スレッドへの切り替えは受け取る側で行う。</remarks>
    public event EventHandler? Changed;

    /// <summary>ファイルを読めなかったときのメッセージ。正常なら null。</summary>
    /// <remarks>ロック・権限などで読めなかったとき。元のファイルを上書きで消さないよう、このときは保存を止める（保存しようとすると例外）。</remarks>
    public string? LoadError { get; private set; }

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。通常は null。</summary>
    public string? RecoveryMessage { get; private set; }

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

    /// <summary>リマインダー本体を保存する（追加・更新）</summary>
    /// <param name="reminder">保存するリマインダー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した内容（採番後）の複製</returns>
    /// <remarks>
    /// <see cref="Reminder.Seq"/> が 0 なら新規として採番し、<see cref="Reminder.No"/> も同じ値にする。
    /// それ以外は同じ <see cref="Reminder.Seq"/> の既存分を更新する（参照番号は既存のまま。論理削除済みなら削除フラグを解除する）。
    /// </remarks>
    /// <exception cref="ArgumentException">指定の連番のリマインダーが無い。</exception>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task<Reminder> SaveAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        Reminder saved;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, _) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            if (reminder.Seq == 0)
            {
                var seq = reminders.Count == 0 ? 1 : reminders.Max(item => item.Seq) + 1;
                saved = reminder with { Seq = seq, No = seq };
                reminders.Add(saved);
            }
            else
            {
                var index = reminders.FindIndex(item => item.Seq == reminder.Seq);
                if (index < 0)
                {
                    throw new ArgumentException($"連番 {reminder.Seq} のリマインダーがありません。", nameof(reminder));
                }
                saved = reminder with { No = reminders[index].No, IsDeleted = false };
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
    /// <param name="seq">削除するリマインダーの連番</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があれば true</returns>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public Task<bool> DeleteAsync(int seq, CancellationToken cancellationToken = default)
        => UpdateRemindersAsync(reminders =>
        {
            var index = reminders.FindIndex(item => item.Seq == seq);
            if (index < 0 || reminders[index].IsDeleted)
            {
                return index >= 0;
            }
            reminders[index] = reminders[index] with { IsDeleted = true };
            return true;
        }, cancellationToken);

    /// <summary>リマインダー本体を物理削除する（一覧から完全に取り除く）</summary>
    /// <param name="seq">削除するリマインダーの連番</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があれば true</returns>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public Task<bool> PurgeAsync(int seq, CancellationToken cancellationToken = default)
        => UpdateRemindersAsync(reminders => reminders.RemoveAll(item => item.Seq == seq) > 0, cancellationToken);

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
    /// <param name="baseNo">リマインダー本体の参照番号</param>
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
    /// <param name="baseNo">リマインダー本体の参照番号</param>
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

        List<string> errors = [];
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
            _loadException = ex;
            errors.Add(ex.Message);
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
            _loadException ??= ex;
            errors.Add(ex.Message);
        }

        LoadError = errors.Count > 0 ? string.Join("\n", errors) : null;
        RecoveryMessage = recoveries.Count > 0 ? string.Join("\n", recoveries) : null;
        return (_reminders, _states);
    }

    /// <summary>読み込みに失敗していたら、保存させずに例外にする</summary>
    /// <exception cref="DataFileException">読み込みに失敗している。</exception>
    private void ThrowIfLoadFailed()
    {
        if (_loadException is not null)
        {
            throw new DataFileException($"{LoadError}\nファイルを読めなかったため、元のデータを消さないよう保存を止めています。アプリを再起動してください。", _loadException);
        }
    }
}
