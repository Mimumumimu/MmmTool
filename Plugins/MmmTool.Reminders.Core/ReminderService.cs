using MmmSdk.Core.Components.Storage;

namespace MmmTool.Reminders.Core;

/// <summary>
/// リマインダー (本体・対応状態)を読み書きする。アプリ全体で 1 つ。
/// </summary>
/// <param name="repository">リマインダーの保存先</param>
/// <remarks>
/// 保存先 (<see cref="IReminderRepository"/>)の 1 件単位の操作に、業務の決まり (論理削除・物理削除・今日の対象の組み立て)を載せる。
/// 一覧は持たず、そのつど保存先から読む (番号の採番・排他・読み込みの失敗の扱いは保存先の仕事)。
/// </remarks>
public sealed class ReminderService(IReminderRepository repository)
{
    /// <summary>時刻が正しくないリマインダーがあるときの警告。無ければ null</summary>
    private volatile string? _timeWarning;

    /// <summary>本体・状態のどちらかが変わった</summary>
    /// <remarks>保存したスレッドから発火する (任意のスレッドになりうる)。UI スレッドへの切り替えは受け取る側で行う。</remarks>
    public event EventHandler? Changed;

    /// <summary>ファイルを読めなかったときのメッセージ。正常なら null。</summary>
    /// <remarks>ロック・権限などで読めなかったとき (最初に読んだあとに分かる)。元のファイルを上書きで消さないよう、このときは保存を止める (保存しようとすると例外)。</remarks>
    public string? LoadError => repository.LoadError;

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。通常は null。</summary>
    /// <remarks>最初に読んだあとに分かる。</remarks>
    public string? RecoveryMessage => repository.RecoveryMessage;

    /// <summary>時刻が正しくないリマインダーがあるときの警告 (番号つき)。無ければ null。</summary>
    /// <remarks>
    /// リマインダーを読むたびに更新する。ファイルの退避 (<see cref="RecoveryMessage"/>)とは別の問題なので、別のプロパティにする。
    /// 手で編集した JSON の時刻が正しくないと、「常に発動済み」のように誤って動くため、通知・今日の対象から外している (本体は消さないので、編集して直せる)。
    /// </remarks>
    public string? TimeWarning => _timeWarning;

    /// <summary>リマインダー本体の一覧を取得する</summary>
    /// <param name="includeDeleted">論理削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>リマインダー本体の一覧</returns>
    public async Task<IReadOnlyList<Reminder>> GetRemindersAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var reminders = await repository.GetRemindersAsync(includeDeleted, cancellationToken);
        UpdateTimeWarning(reminders);
        return reminders;
    }

    /// <summary>ある日の対象 (その日に発生するリマインダー)と、その日の対応状態を取得する</summary>
    /// <param name="now">基準にする日時 (この日付を「今日」とする)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>その日の対象 (時刻 → 参照番号の順)。論理削除済みは含めない。別の日の状態は未対応として扱う</returns>
    /// <remarks>画面 (メイン画面)と通知 (<see cref="ReminderMonitor"/>)が同じ内容を見るよう、「今日の状態」の組み立てはここだけで行う。</remarks>
    public async Task<IReadOnlyList<ReminderTarget>> GetTargetsAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var reminders = await repository.GetRemindersAsync(includeDeleted: false, cancellationToken);
        var states = await repository.GetStatesAsync(cancellationToken);
        UpdateTimeWarning(reminders);

        var today = DateOnly.FromDateTime(now);
        var todayValue = ReminderDates.ToDateValue(now);
        var statuses = states
            .Where(state => state.Date == todayValue)
            .GroupBy(state => state.BaseNo)
            .ToDictionary(group => group.Key, group => group.Last().Status);
        return [.. reminders
            .Where(reminder => ReminderDates.ToTime(reminder.Time) is not null && ReminderDates.OccursOn(reminder, today))
            .OrderBy(reminder => reminder.Time)
            .ThenBy(reminder => reminder.No)
            .Select(reminder => new ReminderTarget(reminder, statuses.GetValueOrDefault(reminder.No)))];
    }

    /// <summary>リマインダー本体を保存する (追加・更新)</summary>
    /// <param name="reminder">保存するリマインダー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した内容 (採番後)</returns>
    /// <remarks>
    /// <see cref="Reminder.No"/> が 0 なら新規として追加する (番号は保存先が決める)。
    /// それ以外は同じ <see cref="Reminder.No"/> の既存分を更新する (論理削除済みなら削除フラグを解除する)。
    /// </remarks>
    /// <exception cref="ArgumentException">指定の番号のリマインダーが無い。</exception>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task<Reminder> SaveAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        Reminder saved;
        if (reminder.No == 0)
        {
            saved = await repository.AddAsync(reminder with { IsDeleted = false }, cancellationToken);
        }
        else
        {
            saved = reminder with { IsDeleted = false };
            if (!await repository.UpdateAsync(saved, cancellationToken))
            {
                throw new ArgumentException($"番号 {reminder.No} のリマインダーがありません。", nameof(reminder));
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return saved;
    }

    /// <summary>リマインダー本体を論理削除する (削除フラグを立てる)</summary>
    /// <param name="no">削除するリマインダーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があれば true</returns>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task<bool> DeleteAsync(int no, CancellationToken cancellationToken = default)
    {
        var found = await repository.SetDeletedAsync(no, isDeleted: true, cancellationToken);
        if (found)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return found;
    }

    /// <summary>リマインダー本体を物理削除する (一覧から完全に取り除く)</summary>
    /// <param name="no">削除するリマインダーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があれば true</returns>
    /// <remarks>対応状態 (<see cref="ReminderState"/>)も一緒に消える (保存先の仕事。理由は <see cref="IReminderRepository.PurgeAsync"/>)。</remarks>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task<bool> PurgeAsync(int no, CancellationToken cancellationToken = default)
    {
        var purged = await repository.PurgeAsync(no, cancellationToken);
        if (purged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return purged;
    }

    /// <summary>リマインダーの対応状態を保存する</summary>
    /// <param name="changes">変更する対応状態</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>同じ参照番号の状態は 1 件に保ち、あれば対象日・値を上書きする。無ければ追加する。まとめて 1 回で保存し、変更の通知も 1 回。</remarks>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public async Task SetStatesAsync(IReadOnlyList<ReminderStateChange> changes, CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return;
        }

        await repository.SetStatesAsync(changes, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>リマインダー 1 件の対応状態を保存する</summary>
    /// <param name="baseNo">リマインダー本体の番号</param>
    /// <param name="date">対象日 (yyyyMMdd の整数)</param>
    /// <param name="status">対応状態</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    public Task SetStateAsync(int baseNo, int date, ReminderStatus status, CancellationToken cancellationToken = default)
        => SetStatesAsync([new ReminderStateChange(baseNo, date, status)], cancellationToken);

    /// <summary>読んだリマインダーから、時刻が正しくないものの警告を更新する</summary>
    /// <param name="reminders">読んだリマインダー</param>
    private void UpdateTimeWarning(IReadOnlyList<Reminder> reminders)
    {
        var invalidTimeNos = reminders
            .Where(reminder => !reminder.IsDeleted && ReminderDates.ToTime(reminder.Time) is null)
            .Select(reminder => reminder.No)
            .ToList();
        _timeWarning = invalidTimeNos.Count > 0
            ? $"時刻が正しくないリマインダーがあります (番号 {string.Join("、", invalidTimeNos)})。通知されません。編集して直してください。"
            : null;
    }
}
