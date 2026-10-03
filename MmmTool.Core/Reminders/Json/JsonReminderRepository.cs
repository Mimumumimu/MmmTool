using MmmSdk.Core.Storage;

namespace MmmTool.Core.Reminders.Json;

/// <summary>リマインダーを JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
/// <remarks>
/// 本体は <c>Reminders.json</c>、対応状態は <c>ReminderStates.json</c> に分けて保存する（汎用設定ストアとは別）。
/// 最初のアクセスで 1 度だけ読み込み、メモリに持つ。書き込みは、変更後のコピーをファイルに書き、成功してからメモリを差し替える（保存に失敗したとき、メモリだけが新しい状態にならない）。
/// 番号は、ロックの中で「最大 + 1」を計算して決める。
/// ファイルが無い・空・壊れているときは空の一覧として扱う（壊れていたファイルは退避して <see cref="RecoveryMessage"/> に残す）。
/// ロック・権限などで読めなかったときも空の一覧として扱うが、元のファイルを空で上書きしないよう、書き込みは止める（書こうとすると例外）。
/// </remarks>
public sealed class JsonReminderRepository(IJsonFileStore store) : IReminderRepository
{
    /// <summary>本体の保存先のファイル名</summary>
    private const string RemindersFileName = "Reminders.json";

    /// <summary>対応状態の保存先のファイル名</summary>
    private const string StatesFileName = "ReminderStates.json";

    /// <summary>読み書きを順番に行うためのロック</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>読み込みの結果（失敗したか・壊れたファイルを退避したか）</summary>
    private readonly LoadStatus _status = new();

    /// <summary>リマインダー本体の一覧（論理削除済みも含む）。読み込むまでは null</summary>
    /// <remarks>書き換えず、保存に成功したときに、新しい一覧へ差し替える。</remarks>
    private List<Reminder>? _reminders;

    /// <summary>対応状態の一覧。読み込むまでは null</summary>
    /// <remarks>書き換えず、保存に成功したときに、新しい一覧へ差し替える。</remarks>
    private List<ReminderState>? _states;

    /// <inheritdoc />
    public string? LoadError => _status.LoadError;

    /// <inheritdoc />
    public string? RecoveryMessage => _status.RecoveryMessage;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reminder>> GetRemindersAsync(bool includeDeleted, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, _) = await EnsureLoadedAsync(cancellationToken);
            return [.. reminders.Where(reminder => includeDeleted || !reminder.IsDeleted)];
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReminderState>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (_, states) = await EnsureLoadedAsync(cancellationToken);
            return [.. states];
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Reminder> AddAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, _) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            var added = reminder with { No = reminders.Count == 0 ? 1 : reminders.Max(item => item.No) + 1 };
            await SaveRemindersAsync([.. reminders, added], cancellationToken);
            return added;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, _) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            var index = reminders.FindIndex(item => item.No == reminder.No);
            if (index < 0)
            {
                return false;
            }

            var next = new List<Reminder>(reminders) { [index] = reminder };
            await SaveRemindersAsync(next, cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetDeletedAsync(int no, bool isDeleted, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, _) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            var index = reminders.FindIndex(item => item.No == no);
            if (index < 0)
            {
                return false;
            }
            if (reminders[index].IsDeleted == isDeleted)
            {
                return true;
            }

            var next = new List<Reminder>(reminders) { [index] = reminders[index] with { IsDeleted = isDeleted } };
            await SaveRemindersAsync(next, cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> PurgeAsync(int no, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (reminders, states) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            if (!reminders.Exists(item => item.No == no))
            {
                return false;
            }

            if (states.Exists(state => state.BaseNo == no))
            {
                await SaveStatesAsync([.. states.Where(state => state.BaseNo != no)], cancellationToken);
            }
            await SaveRemindersAsync([.. reminders.Where(item => item.No != no)], cancellationToken);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetStatesAsync(IReadOnlyList<ReminderStateChange> changes, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var (_, states) = await EnsureLoadedAsync(cancellationToken);
            ThrowIfLoadFailed();

            var next = new List<ReminderState>(states);
            foreach (var change in changes)
            {
                var index = next.FindIndex(state => state.BaseNo == change.BaseNo);
                if (index >= 0)
                {
                    next[index] = next[index] with { Date = change.Date, Status = change.Status };
                }
                else
                {
                    var seq = next.Count == 0 ? 1 : next.Max(state => state.Seq) + 1;
                    next.Add(new ReminderState { Seq = seq, BaseNo = change.BaseNo, Date = change.Date, Status = change.Status });
                }
            }

            await SaveStatesAsync(next, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>本体の一覧をファイルに書き、成功したらメモリの一覧を差し替える</summary>
    /// <param name="next">書き込む一覧</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>呼ぶ側は <c>_lock</c> を取っておくこと。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。このときメモリの一覧は変えない。</exception>
    private async Task SaveRemindersAsync(List<Reminder> next, CancellationToken cancellationToken)
    {
        await store.WriteAsync(RemindersFileName, new ReminderFile { Items = next }, ReminderJsonContext.Readable.ReminderFile, cancellationToken);
        _reminders = next;
    }

    /// <summary>対応状態の一覧をファイルに書き、成功したらメモリの一覧を差し替える</summary>
    /// <param name="next">書き込む一覧</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>呼ぶ側は <c>_lock</c> を取っておくこと。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。このときメモリの一覧は変えない。</exception>
    private async Task SaveStatesAsync(List<ReminderState> next, CancellationToken cancellationToken)
    {
        await store.WriteAsync(StatesFileName, new ReminderStateFile { Items = next }, ReminderJsonContext.Readable.ReminderStateFile, cancellationToken);
        _states = next;
    }

    /// <summary>未読み込みなら読み込む。呼ぶ側は <c>_lock</c> を取っておくこと</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>メモリに持っているリマインダー本体と対応状態の一覧</returns>
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
            var result = await store.ReadAsync(RemindersFileName, ReminderJsonContext.Readable.ReminderFile, cancellationToken);
            _reminders = result.Value?.Items ?? [];
            if (result.RecoveryMessage is not null) recoveries.Add(result.RecoveryMessage);
        }
        catch (DataFileException ex)
        {
            _reminders = [];
            failures.Add(ex);
        }

        try
        {
            var result = await store.ReadAsync(StatesFileName, ReminderJsonContext.Readable.ReminderStateFile, cancellationToken);
            _states = result.Value?.Items ?? [];
            if (result.RecoveryMessage is not null) recoveries.Add(result.RecoveryMessage);
        }
        catch (DataFileException ex)
        {
            _states = [];
            failures.Add(ex);
        }

        _status.Record(failures, recoveries);
        return (_reminders, _states);
    }

    /// <summary>読み込みに失敗していたら、書き込ませずに例外にする</summary>
    /// <exception cref="DataFileException">読み込みに失敗している。</exception>
    private void ThrowIfLoadFailed()
        => _status.ThrowIfSaveBlocked("ファイルを読めなかったため、元のデータを消さないよう保存を止めています。アプリを再起動してください。");
}
