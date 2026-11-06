using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Scheduling;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Reminders.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>
/// リマインダーを、SQL Server の <c>dbo.Reminder</c>・<c>dbo.ReminderState</c> に保存する。
/// </summary>
/// <remarks>
/// <para>
/// 読みはメモリから返す。通知の毎分の判定で、DB を毎回読まないため。見えるのは、宛先が自分のもの・全員宛て・自分が作成したリマインダーと、自分の対応状態だけ。通知と今日の対象にするのは、宛先が自分と全員宛てだけで、これは <see cref="ReminderService"/> が絞る。
/// 1 分ごとに、前回より後に更新された行だけを読んでメモリに重ね、前回と違えば <see cref="ExternalChanged"/> で知らせる。ほかの PC の変更を知るため。
/// 最初と、ユーザーが替わったときと、管理者の無効の状態が変わったときと、差分を 30 回続けたときは、見える分を全件読み直す。
/// </para>
/// <para>
/// 書きは、トランザクションで DB に書き、成功したら全件を読み直してメモリに反映する。DB の内容と同じ形になり、備考・リンクの空文字は null などになる。
/// 読み込みに失敗したときは、最後に読めた内容を返し続け、<see cref="LoadError"/> に残す。次の読み直しで、つながれば消える。書き込みの失敗は、
/// 画面に出せるメッセージの <see cref="DataFileException"/>。同時編集の上書きは検出しない。
/// </para>
/// <para>
/// 操作した人 (作成者・更新者・対応状態の持ち主)は <see cref="CurrentUser"/>。特定していないと、読み込めず、保存もできない。
/// 編集は見える人なら誰でもでき、削除済みを編集すると復活する。論理削除は作成者だけができる。
/// 完全削除は、DB では行わない。<see cref="CanPurge"/> が false になる。未対応への変更も、対応状態の行を消さず、<c>Status</c> を 0 にする。
/// </para>
/// </remarks>
public sealed class SqlServerReminderRepository : IReminderRepository, IDisposable
{
    /// <summary>差分の読み出しで、前回の基準より戻して読む時間。更新が確定するのが遅れた書き込みを、取りこぼさないため</summary>
    private static readonly TimeSpan DiffOverlap = TimeSpan.FromSeconds(10);

    /// <summary>差分の読み出しを何回続けたら、全件を読み直すか。見えなくなった行など、差分では分からない変わりを直すため</summary>
    private const int FullReloadEvery = 30;

    /// <summary>SQL Server への入口</summary>
    private readonly SqlServerDatabase _database;

    /// <summary>今のユーザー</summary>
    private readonly CurrentUser _currentUser;

    /// <summary>ログイン</summary>
    private readonly UserSignInService _signIn;

    /// <summary>現在の日付を知るための時計</summary>
    private readonly TimeProvider _time;

    /// <summary>毎分の合図</summary>
    private readonly IMinuteTicker _ticker;

    /// <summary>ほかの PC の変更を読み直す合図の登録。最初の読み込みの前は null</summary>
    private IMinuteTickSubscription? _poll;

    /// <summary>読み書きと、読み直しを順番に行うためのロック</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>読み直しの繰り返しを止める</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>見えるリマインダー本体の一覧で、論理削除済みも含む。書き換えず、読み直したときに差し替える</summary>
    private volatile IReadOnlyList<Reminder> _reminders = [];

    /// <summary>自分の対応状態の一覧。書き換えず、読み直したときに差し替える</summary>
    private volatile IReadOnlyList<ReminderState> _states = [];

    /// <summary>最初の読み込みと、読み直しの繰り返しを始めたか</summary>
    private volatile bool _started;

    /// <summary>破棄したか (0 = まだ、1 = 破棄した)</summary>
    private int _disposed;

    /// <summary>最後の読み込みに失敗したときのメッセージ。正常なら null</summary>
    private volatile string? _loadError;

    /// <summary>前回の読み出しを始めたときの、DB の時計の日時。差分の読み出しの基準。まだ読んでいなければ null</summary>
    private DateTimeOffset? _readAt;

    /// <summary>前回の読み出しで、管理者にリマインダーを無効にされていたか</summary>
    private bool _wasDisabled;

    /// <summary>前回の読み出しが、どの人の分か。人が替わったら、全件を読み直す</summary>
    private int _loadedUserId;

    /// <summary>前回の全件の読み出しから、差分で読んだ回数</summary>
    private int _diffReads;

    /// <summary>DB に保存するリマインダーの保存先を作る</summary>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="currentUser">今のユーザー</param>
    /// <param name="signIn">ログイン (起動時にログインできなかったとき、読み直しのたびに、覚えているログイン名とパスワードで、もう一度試す)</param>
    /// <param name="time">現在の日付を知るための時計</param>
    /// <param name="ticker">毎分の合図。ほかの PC の変更を、毎分 1 回読み直すのに使う</param>
    public SqlServerReminderRepository(
        SqlServerDatabase database, CurrentUser currentUser, UserSignInService signIn, TimeProvider time, IMinuteTicker ticker)
    {
        _database = database;
        _currentUser = currentUser;
        _signIn = signIn;
        _time = time;
        _ticker = ticker;
        _currentUser.Changed += OnCurrentUserChanged;
    }

    /// <inheritdoc />
    /// <remarks>DB を読めなかったとき。ユーザーを特定していない場合も含む。最後に読めた内容は、そのまま使える。</remarks>
    public string? LoadError => _loadError;

    /// <inheritdoc />
    /// <remarks>DB は壊れたファイルを退避しないので、常に null。</remarks>
    public string? RecoveryMessage => null;

    /// <inheritdoc />
    /// <remarks>特定していないときは 0。</remarks>
    public int CurrentUserId => _currentUser.User?.Id ?? 0;

    /// <inheritdoc />
    /// <remarks>DB は、行を消さない方針 (論理削除だけ)なので、false。</remarks>
    public bool CanPurge => false;

    /// <inheritdoc />
    public event EventHandler? ExternalChanged;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reminder>> GetRemindersAsync(bool includeDeleted, CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        var all = _reminders;
        return includeDeleted ? all : [.. all.Where(reminder => !reminder.IsDeleted)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReminderState>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        return _states;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">日付・時刻・曜日・宛先が、DB に入れられない値。</exception>
    public async Task<Reminder> AddAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        var row = ReminderRow.FromReminder(reminder with { No = 0 });
        var id = await WriteAsync(
            (connection, transaction, userId) => ReminderSql.InsertAsync(connection, transaction, userId, row, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return (row with { Id = id, CreatedByUserId = _currentUser.Id, AuthorUserId = _currentUser.Id }).ToReminder();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">日付・時刻・曜日・宛先が、DB に入れられない値。</exception>
    public Task<bool> UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        var row = ReminderRow.FromReminder(reminder);
        // 編集できるのは、宛先が自分・全員宛て・自分が作成したもので、見える人だけ。削除済みを編集すると、復活する。呼ぶ側が IsDeleted を false にして渡す
        return WriteAsync(
            (connection, transaction, userId) => ReminderSql.UpdateAsync(connection, transaction, userId, row, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>見える人 (宛先が自分・全員宛て・自分が作成したもの)なら、誰でも変えられる。編集と同じ。見えないときは、対象が無いのと同じで、false を返す。</remarks>
    public Task<bool> SetDeletedAsync(int no, bool isDeleted, CancellationToken cancellationToken = default)
        => WriteAsync(
            (connection, transaction, userId) => ReminderSql.SetDeletedAsync(connection, transaction, userId, no, isDeleted, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>DB は、行を消さない方針 (論理削除だけ)なので、できない。画面は、<see cref="CanPurge"/> が false のとき、操作を出さない。</remarks>
    /// <exception cref="NotSupportedException">常に。呼ぶ側のバグ。</exception>
    public Task<bool> PurgeAsync(int no, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DB モードでは、完全削除はできません (論理削除だけです)。");

    /// <inheritdoc />
    /// <remarks>
    /// 未対応 (<see cref="ReminderStatus.None"/>)に戻したときも、行は消さない。<c>Status</c> を 0 にして残す。
    /// 完了・スヌーズ・未対応のどれも、あれば上書きし、無ければ追加する。全部を 1 つのトランザクションで書く。
    /// </remarks>
    /// <exception cref="ArgumentException">対応状態の値・日付が、DB に入れられない値。</exception>
    public Task SetStatesAsync(IReadOnlyList<ReminderStateChange> changes, CancellationToken cancellationToken = default)
    {
        // 行にする値を、先に確かめる (書き始めてから、途中で例外にしないため)
        var rows = changes
            .Select(change => (Change: change, Row: ReminderStateRow.FromReminderState(new ReminderState { BaseNo = change.BaseNo, Date = change.Date, Status = change.Status })))
            .ToList();

        return WriteAsync(async (connection, transaction, userId) =>
        {
            foreach (var (change, row) in rows)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                ReminderSql.AddUserParameter(command, userId);
                command.Parameters.Add(new SqlParameter("@ReminderId", SqlDbType.Int) { Value = change.BaseNo });
                command.CommandText =
                    """
                    UPDATE dbo.ReminderState WITH (UPDLOCK, HOLDLOCK)
                    SET Date = @Date, Status = @Status, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                    WHERE ReminderId = @ReminderId AND UserId = @UserId;
                    IF @@ROWCOUNT = 0
                        INSERT INTO dbo.ReminderState (CreatedByUserId, UpdatedByUserId, ReminderId, UserId, Date, Status)
                        VALUES (@UserId, @UserId, @ReminderId, @UserId, @Date, @Status);
                    """;
                command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = row.Date.ToDateTime(TimeOnly.MinValue) });
                command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = row.Status });
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            return 0;
        }, cancellationToken);
    }

    /// <summary>読み直しの繰り返しを止める</summary>
    /// <remarks>何度呼んでもよい。DI が、同じインスタンスを、実体の型とインターフェースの両方の登録から、2 回破棄するため。</remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _currentUser.Changed -= OnCurrentUserChanged;
        _poll?.Dispose();
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>今のユーザーが設定されたら、すぐに読み直す (ログインした直後に、「ログインが必要」が、次の読み直しまで残らないように)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 読み直しの中でユーザーを特定したとき (ロックを持っている間)にも呼ばれるので、ロックを同期で待たず、別に読み直しを始める
    /// すでに読み直した内容と同じなら、何も通知しない。最初の読み込みがまだなら、そちらが読むので、何もしない。
    /// </remarks>
    private void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        if (!_started || _disposed != 0)
        {
            return;
        }

        ReloadAndNotifyAsync().Forget();
    }

    /// <summary>読み直して、変わっていれば <see cref="ExternalChanged"/> で知らせる</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    private async Task ReloadAndNotifyAsync()
    {
        bool changed;
        try
        {
            await _lock.WaitAsync(_stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            changed = await ReloadAsync(_stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            _lock.Release();
        }

        if (changed)
        {
            ExternalChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>最初の読み込みと、読み直しの繰り返しを、まだなら始める</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>始めた (または、すでに始めていた)ことを表すタスク</returns>
    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }
            await ReloadAsync(cancellationToken).ConfigureAwait(false);
            _started = true;
            // ほかの PC の変更は、毎分 1 回読み直す。00 秒にそろえると、全員の端末が同じ 1 秒に DB へ読みに来るので、ずらした合図で受ける
            _poll = _ticker.Subscribe(_ => ReloadAndNotifyAsync(), new MinuteTickOptions { Timing = MinuteTickTiming.Staggered });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>見える分を全件読み直して、メモリに反映する (ロックを持って呼ぶ)</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>内容が変わった (または、読み込みの失敗の有無が変わった)なら true</returns>
    private async Task<bool> ReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 起動時にログインできなかった (DB に届かなかった・まだログインしていない)ときは、読み直しのたびに、もう一度試す
            if (!_currentUser.IsIdentified
                && await _signIn.TrySignInWithSavedAsync(DateOnly.FromDateTime(_time.GetLocalNow().DateTime), cancellationToken).ConfigureAwait(false) is null)
            {
                return SetLoadError("ユーザーが特定されていないため、リマインダーを読み込めず、保存もできません (ログインが必要です)。");
            }

            var userId = _currentUser.Id;

            // 最初・人が替わったとき・一定の回数ごと・無効の状態が変わったときは、全件を読む。それ以外は、前回より後に更新された行だけを読む
            var needsFull = _readAt is null || _loadedUserId != userId || _diffReads >= FullReloadEvery;
            Snapshot? snapshot = null;
            if (!needsFull)
            {
                snapshot = await _database
                    .RunAsync(connection => ReadChangesAsync(connection, userId, _readAt!.Value - DiffOverlap, _wasDisabled, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (snapshot is null)
            {
                snapshot = await _database
                    .RunAsync(connection => ReadAllAsync(connection, userId, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
                _diffReads = 0;
            }
            else
            {
                snapshot = Merge(snapshot, _reminders, _states);
                _diffReads++;
            }

            var changed = !snapshot.Reminders.SequenceEqual(_reminders) || !snapshot.States.SequenceEqual(_states) || _loadError is not null;
            _reminders = snapshot.Reminders;
            _states = snapshot.States;
            _readAt = snapshot.ReadAt;
            _wasDisabled = snapshot.Disabled;
            _loadedUserId = userId;
            _loadError = null;
            return changed;
        }
        catch (DataFileException ex)
        {
            return SetLoadError($"データベースから読み込めませんでした (最後に読めた内容を使っています)。{ex.Message}");
        }
    }

    /// <summary>読み込みに失敗したときのメッセージを記録する</summary>
    /// <param name="message">メッセージ</param>
    /// <returns>前回と違えば true</returns>
    private bool SetLoadError(string message)
    {
        var changed = _loadError != message;
        _loadError = message;
        return changed;
    }

    /// <summary>読み出しの結果。見えるリマインダーと、自分の対応状態と、読み始めた DB の時計の日時</summary>
    /// <param name="Reminders">リマインダー本体。全件の読み出しでは全部で、差分の読み出しでは、更新された行だけ</param>
    /// <param name="States">自分の対応状態。全件の読み出しでは全部で、差分の読み出しでは、更新された行だけ</param>
    /// <param name="ReadAt">読み始めたときの、DB の時計の日時。次の差分の基準になる</param>
    /// <param name="Disabled">管理者に、リマインダーを無効にされているか</param>
    private sealed record Snapshot(IReadOnlyList<Reminder> Reminders, IReadOnlyList<ReminderState> States, DateTimeOffset ReadAt, bool Disabled);

    /// <summary>DB の今の日時と、管理者にリマインダーを無効にされているかを読む問い合わせ</summary>
    private static readonly string TimeAndDisabledSql =
        $"SELECT SYSDATETIMEOFFSET(), CASE WHEN {FeatureAccessSql.IsDisabled("@UserId", CodeFeatures.Reminders)} THEN 1 ELSE 0 END";

    /// <summary>リマインダー本体の列を選ぶ問い合わせの先頭部分</summary>
    private const string ReminderSelect = ReminderSql.Select;

    /// <summary>対応状態の列を選ぶ問い合わせの先頭部分</summary>
    private const string StateSelect = "SELECT Id, ReminderId, Date, Status FROM dbo.ReminderState";

    /// <summary>見えるリマインダーと、自分の対応状態を、全件読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>全件の読み出しの結果</returns>
    private static async Task<Snapshot> ReadAllAsync(SqlConnection connection, int userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        // 管理者がリマインダーを無効にした人には、何も見えない (通知も出ない)
        command.CommandText =
            $"""
            {TimeAndDisabledSql};
            {ReminderSelect}
            WHERE (TargetUserId IN (0, @UserId) OR AuthorUserId = @UserId) AND NOT {FeatureAccessSql.IsDisabled("@UserId", CodeFeatures.Reminders)}
            ORDER BY Id;
            {StateSelect}
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY Id;
            """;
        ReminderSql.AddUserParameter(command, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await ReadSnapshotAsync(reader, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>前回の基準より後に更新された、リマインダーと対応状態だけを読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="since">この日時以降に更新された行を読む</param>
    /// <param name="wasDisabled">前回の読み出しで、無効にされていたか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新された行。無効の状態が前回と変わったとき (有効に戻ったときも含む)は null で、全件を読み直す。無効のままのときは、リマインダーを空にして返す</returns>
    /// <remarks>
    /// 3 つの問い合わせを 1 回の往復で読む。見える範囲の絞り込みは、全件のときと同じ。
    /// 更新日時の索引 (<c>IX_Reminder_UpdatedAt</c>・<c>IX_ReminderState_UserId_UpdatedAt</c>)で、更新された行だけを探す。論理削除された行も、更新日時が進むので届く。
    /// </remarks>
    private static async Task<Snapshot?> ReadChangesAsync(
        SqlConnection connection, int userId, DateTimeOffset since, bool wasDisabled, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            {TimeAndDisabledSql};
            {ReminderSelect}
            WHERE UpdatedAt >= @Since AND (TargetUserId IN (0, @UserId) OR AuthorUserId = @UserId)
            ORDER BY Id;
            {StateSelect}
            WHERE UserId = @UserId AND IsDeleted = 0 AND UpdatedAt >= @Since
            ORDER BY Id;
            """;
        ReminderSql.AddUserParameter(command, userId);
        command.Parameters.Add(new SqlParameter("@Since", SqlDbType.DateTimeOffset, 0) { Value = since, Scale = 3 });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await ReadSnapshotAsync(reader, cancellationToken).ConfigureAwait(false);

        // 無効の状態が変わったときは、見える分が丸ごと変わるので、差分では足りない
        if (snapshot.Disabled != wasDisabled)
        {
            return null;
        }

        // 無効のままの人には、何も見えない。差分の問い合わせは無効を見ないので、読んだリマインダーは捨てる
        return snapshot.Disabled ? snapshot with { Reminders = [] } : snapshot;
    }

    /// <summary>3 つの結果 (日時と無効の有無・リマインダー・対応状態)を、順に読む</summary>
    /// <param name="reader">読んでいるリーダー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み出しの結果</returns>
    private static async Task<Snapshot> ReadSnapshotAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var readAt = reader.GetDateTimeOffset(0);
        var disabled = reader.GetInt32(1) == 1;

        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
        var reminders = new List<Reminder>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            reminders.Add(ReminderSql.ReadReminder(reader));
        }

        await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
        var states = new List<ReminderState>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            states.Add(new ReminderStateRow
            {
                Id = reader.GetInt32(0),
                ReminderId = reader.GetInt32(1),
                Date = DateOnly.FromDateTime(reader.GetDateTime(2)),
                Status = reader.GetByte(3),
            }.ToReminderState());
        }

        return new Snapshot(reminders, states, readAt, disabled);
    }

    /// <summary>差分で読んだ行を、メモリの一覧に重ねる</summary>
    /// <param name="changes">差分の読み出しの結果</param>
    /// <param name="reminders">メモリのリマインダー</param>
    /// <param name="states">メモリの対応状態</param>
    /// <returns>重ねたあとの全件の一覧。番号の順</returns>
    private static Snapshot Merge(Snapshot changes, IReadOnlyList<Reminder> reminders, IReadOnlyList<ReminderState> states)
    {
        var mergedReminders = reminders.ToDictionary(reminder => reminder.No);
        foreach (var reminder in changes.Reminders)
        {
            mergedReminders[reminder.No] = reminder;
        }

        var mergedStates = states.ToDictionary(state => state.Seq);
        foreach (var state in changes.States)
        {
            mergedStates[state.Seq] = state;
        }

        return changes with
        {
            Reminders = [.. mergedReminders.Values.OrderBy(reminder => reminder.No)],
            States = [.. mergedStates.Values.OrderBy(state => state.Seq)],
        };
    }

    /// <summary>トランザクションで書き込み、成功したらメモリに反映する</summary>
    /// <typeparam name="T">書き込みの結果の型</typeparam>
    /// <param name="work">開いた接続・トランザクション・今のユーザーの番号で行う書き込み</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの結果</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない・接続できない・保存できなかった。メッセージは画面に出せる。</exception>
    private async Task<T> WriteAsync<T>(Func<SqlConnection, SqlTransaction, int, Task<T>> work, CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 起動時にログインできていなかったときは、保存の前に、もう一度試す (ログインしたあと・DB が復旧したあとの最初の保存のため)
            if (!_currentUser.IsIdentified)
            {
                await ReloadAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!_currentUser.IsIdentified)
            {
                // ログインできない理由 (DB に届かない・ログインしていない)は、読み込みの失敗と同じ
                throw new DataFileException(
                    _loadError ?? "ユーザーが特定されていないため、保存できません (ログインが必要です)。",
                    new InvalidOperationException("今のユーザーが特定されていません。"));
            }

            var userId = _currentUser.Id;
            var result = await _database.RunAsync(async connection =>
            {
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var value = await work(connection, transaction, userId).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return value;
            }, cancellationToken).ConfigureAwait(false);

            // 書いた内容を、DB の内容 (正規化したあとの形)でメモリに反映する。読み直しに失敗しても、書き込みは成功している (失敗は LoadError に残る)
            await ReloadAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }
}
