using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Reminders.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>
/// リマインダーを、SQL Server の <c>dbo.Reminder</c>・<c>dbo.ReminderState</c> に保存する。
/// </summary>
/// <remarks>
/// <para>
/// 読みはメモリから返す (通知の毎分の判定で、DB を毎回読まない)。見えるのは、宛先が自分のもの・全員宛て・自分が作成したリマインダーと、自分の対応状態だけ (通知と今日の対象にするのは、宛先が自分と全員宛てだけで、これは <see cref="ReminderService"/> が絞る)。
/// 1 分ごとに、見える分を全件読み直し、前回と違えば <see cref="ExternalChanged"/> で知らせる (ほかの PC の変更を知るため。完全削除も、読み直しで反映される)。
/// </para>
/// <para>
/// 書きは、トランザクションで DB に書き、成功したら全件を読み直してメモリに反映する (DB の内容と同じ形になる。備考・リンクの空文字は null など)。
/// 読み込みに失敗したときは、最後に読めた内容を返し続け、<see cref="LoadError"/> に残す (次の読み直しで、つながれば消える)。書き込みの失敗は、
/// 画面に出せるメッセージの <see cref="DataFileException"/>。同時編集の上書きは検出しない。
/// </para>
/// <para>
/// 操作した人 (作成者・更新者・対応状態の持ち主)は <see cref="CurrentUser"/>。特定していないと、読み込めず、保存もできない。
/// 編集は見える人なら誰でもでき (削除済みを編集すると復活する)、論理削除は作成者だけができる。完全削除は、DB では行わない (<see cref="CanPurge"/> が false)。未対応への変更は、対応状態の行を消す。
/// </para>
/// </remarks>
public sealed class SqlServerReminderRepository : IReminderRepository, IDisposable
{
    /// <summary>ほかの PC の変更を読み直す間隔の既定値</summary>
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMinutes(1);

    /// <summary>SQL Server への入口</summary>
    private readonly SqlServerDatabase _database;

    /// <summary>今のユーザー</summary>
    private readonly CurrentUser _currentUser;

    /// <summary>ログイン</summary>
    private readonly UserSignInService _signIn;

    /// <summary>現在の日付を知るための時計</summary>
    private readonly TimeProvider _time;

    /// <summary>ほかの PC の変更を読み直す間隔</summary>
    private readonly TimeSpan _pollInterval;

    /// <summary>読み書きと、読み直しを順番に行うためのロック</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>読み直しの繰り返しを止める</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>見えるリマインダー本体の一覧 (論理削除済みも含む)。書き換えず、読み直したときに差し替える</summary>
    private volatile IReadOnlyList<Reminder> _reminders = [];

    /// <summary>自分の対応状態の一覧。書き換えず、読み直したときに差し替える</summary>
    private volatile IReadOnlyList<ReminderState> _states = [];

    /// <summary>最初の読み込みと、読み直しの繰り返しを始めたか</summary>
    private volatile bool _started;

    /// <summary>破棄したか (0 = まだ、1 = 破棄した)</summary>
    private int _disposed;

    /// <summary>最後の読み込みに失敗したときのメッセージ。正常なら null</summary>
    private volatile string? _loadError;

    /// <summary>DB に保存するリマインダーの保存先を作る</summary>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="currentUser">今のユーザー</param>
    /// <param name="signIn">ログイン (起動時にログインできなかったとき、読み直しのたびに、覚えているログイン名とパスワードで、もう一度試す)</param>
    /// <param name="time">現在の日付を知るための時計</param>
    /// <param name="pollInterval">ほかの PC の変更を読み直す間隔。省略すると 1 分</param>
    public SqlServerReminderRepository(
        SqlServerDatabase database, CurrentUser currentUser, UserSignInService signIn, TimeProvider time, TimeSpan? pollInterval = null)
    {
        _database = database;
        _currentUser = currentUser;
        _signIn = signIn;
        _time = time;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        _currentUser.Changed += OnCurrentUserChanged;
    }

    /// <inheritdoc />
    /// <remarks>DB を読めなかったとき (ユーザーを特定していない場合も含む)。最後に読めた内容は、そのまま使える。</remarks>
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
        var id = await WriteAsync(async (connection, transaction, userId) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO dbo.Reminder
                    (IsDeleted, CreatedByUserId, UpdatedByUserId, TargetUserId, Date, Time, Weekdays, Title, Note, Link, IsSpeak)
                OUTPUT INSERTED.Id
                VALUES
                    (@IsDeleted, @UserId, @UserId, @TargetUserId, @Date, @Time, @Weekdays, @Title, @Note, @Link, @IsSpeak)
                """;
            AddUserParameter(command, userId);
            AddReminderParameters(command, row);
            return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }, cancellationToken).ConfigureAwait(false);
        return (row with { Id = id, CreatedByUserId = _currentUser.Id }).ToReminder();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">日付・時刻・曜日・宛先が、DB に入れられない値。</exception>
    public Task<bool> UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        var row = ReminderRow.FromReminder(reminder);
        return WriteAsync(async (connection, transaction, userId) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            // 編集できるのは、見える人 (宛先が自分・全員宛て・自分が作成したもの)。削除済みを編集すると、復活する (呼ぶ側が IsDeleted を false にして渡す)
            command.CommandText =
                """
                UPDATE dbo.Reminder
                SET IsDeleted = @IsDeleted,
                    TargetUserId = @TargetUserId, Date = @Date, Time = @Time, Weekdays = @Weekdays,
                    Title = @Title, Note = @Note, Link = @Link, IsSpeak = @IsSpeak,
                    UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @Id AND (TargetUserId IN (0, @UserId) OR CreatedByUserId = @UserId)
                """;
            AddUserParameter(command, userId);
            AddReminderParameters(command, row);
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = row.Id });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>作成者だけが変えられる。作成者でないときは、対象が無いのと同じ (false)。</remarks>
    public Task<bool> SetDeletedAsync(int no, bool isDeleted, CancellationToken cancellationToken = default)
        => WriteAsync(async (connection, transaction, userId) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE dbo.Reminder
                SET IsDeleted = @IsDeleted, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @Id AND CreatedByUserId = @UserId
                """;
            AddUserParameter(command, userId);
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = no });
            command.Parameters.Add(new SqlParameter("@IsDeleted", SqlDbType.Bit) { Value = isDeleted });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }, cancellationToken);

    /// <inheritdoc />
    /// <remarks>DB は、行を消さない方針 (論理削除だけ)なので、できない。画面は、<see cref="CanPurge"/> が false のとき、操作を出さない。</remarks>
    /// <exception cref="NotSupportedException">常に。呼ぶ側のバグ。</exception>
    public Task<bool> PurgeAsync(int no, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DB モードでは、完全削除はできません (論理削除だけです)。");

    /// <inheritdoc />
    /// <remarks>未対応 (<see cref="ReminderStatus.None"/>)は、行を消す。完了・スヌーズは、あれば上書きし、無ければ追加する。全部を 1 つのトランザクションで書く。</remarks>
    /// <exception cref="ArgumentException">対応状態の値・日付が、DB に入れられない値。</exception>
    public Task SetStatesAsync(IReadOnlyList<ReminderStateChange> changes, CancellationToken cancellationToken = default)
    {
        // 行にする値を、先に確かめる (書き始めてから、途中で例外にしないため)
        var rows = changes
            .Select(change => (Change: change, Row: change.Status == ReminderStatus.None
                ? null
                : ReminderStateRow.FromReminderState(new ReminderState { BaseNo = change.BaseNo, Date = change.Date, Status = change.Status })))
            .ToList();

        return WriteAsync(async (connection, transaction, userId) =>
        {
            foreach (var (change, row) in rows)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                AddUserParameter(command, userId);
                command.Parameters.Add(new SqlParameter("@ReminderId", SqlDbType.Int) { Value = change.BaseNo });
                if (row is null)
                {
                    command.CommandText = "DELETE FROM dbo.ReminderState WHERE ReminderId = @ReminderId AND UserId = @UserId";
                }
                else
                {
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
                }
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            return 0;
        }, cancellationToken);
    }

    /// <summary>読み直しの繰り返しを止める</summary>
    /// <remarks>何度呼んでもよい (DI が、同じインスタンスを、実体の型とインターフェースの両方の登録から、2 回破棄するため)。</remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _currentUser.Changed -= OnCurrentUserChanged;
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>今のユーザーが設定されたら、すぐに読み直す (ログインした直後に、「ログインが必要」が、次の読み直しまで残らないように)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 読み直しの中でユーザーを特定したとき (ロックを持っている間)にも呼ばれるので、ロックを同期で待たず、別に読み直しを始める
    /// (すでに読み直した内容と同じなら、何も通知しない)。最初の読み込みがまだなら、そちらが読むので、何もしない。
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
            PollAsync(_stop.Token).Forget();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>一定の間隔で読み直して、変わっていれば <see cref="ExternalChanged"/> で知らせる</summary>
    /// <param name="cancellationToken">止めるためのトークン (保存先の破棄で止まる)</param>
    /// <returns>繰り返しの完了を表すタスク</returns>
    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                bool changed;
                await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    changed = await ReloadAsync(cancellationToken).ConfigureAwait(false);
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
        }
        catch (OperationCanceledException)
        {
            // 破棄で止まった
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
            var (reminders, states) = await _database
                .RunAsync(connection => ReadAllAsync(connection, userId, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            var changed = !reminders.SequenceEqual(_reminders) || !states.SequenceEqual(_states) || _loadError is not null;
            _reminders = reminders;
            _states = states;
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

    /// <summary>見えるリマインダーと、自分の対応状態を、全件読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>見えるリマインダーと、自分の対応状態</returns>
    private static async Task<(IReadOnlyList<Reminder> Reminders, IReadOnlyList<ReminderState> States)> ReadAllAsync(
        SqlConnection connection, int userId, CancellationToken cancellationToken)
    {
        var reminders = new List<Reminder>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT Id, IsDeleted, CreatedByUserId, TargetUserId, Date, Time, Weekdays, Title, Note, Link, IsSpeak
                FROM dbo.Reminder
                WHERE TargetUserId IN (0, @UserId) OR CreatedByUserId = @UserId
                ORDER BY Id
                """;
            AddUserParameter(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                reminders.Add(new ReminderRow
                {
                    Id = reader.GetInt32(0),
                    IsDeleted = reader.GetBoolean(1),
                    CreatedByUserId = reader.GetInt32(2),
                    TargetUserId = reader.GetInt32(3),
                    Date = DateOnly.FromDateTime(reader.GetDateTime(4)),
                    Time = TimeOnly.FromTimeSpan(reader.GetTimeSpan(5)),
                    Weekdays = reader.GetByte(6),
                    Title = reader.GetString(7),
                    Note = reader.GetString(8),
                    Link = reader.GetString(9),
                    IsSpeak = reader.GetBoolean(10),
                }.ToReminder());
            }
        }

        var states = new List<ReminderState>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT Id, ReminderId, Date, Status
                FROM dbo.ReminderState
                WHERE UserId = @UserId AND IsDeleted = 0
                ORDER BY Id
                """;
            AddUserParameter(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
        }

        return (reminders, states);
    }

    /// <summary>トランザクションで書き込み、成功したらメモリに反映する</summary>
    /// <typeparam name="T">書き込みの結果の型</typeparam>
    /// <param name="work">開いた接続・トランザクション・今のユーザーの番号で行う書き込み</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの結果</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない・接続できない・保存できなかった (メッセージは画面に出せる)。</exception>
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

    /// <summary>今のユーザーの番号のパラメーターを足す</summary>
    /// <param name="command">足す先のコマンド</param>
    /// <param name="userId">今のユーザーの番号</param>
    private static void AddUserParameter(SqlCommand command, int userId)
        => command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });

    /// <summary>リマインダー本体の行のパラメーターを足す (番号・作成者・更新者は含まない)</summary>
    /// <param name="command">足す先のコマンド</param>
    /// <param name="row">リマインダー本体の行</param>
    private static void AddReminderParameters(SqlCommand command, ReminderRow row)
    {
        command.Parameters.Add(new SqlParameter("@IsDeleted", SqlDbType.Bit) { Value = row.IsDeleted });
        command.Parameters.Add(new SqlParameter("@TargetUserId", SqlDbType.Int) { Value = row.TargetUserId });
        command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = row.Date.ToDateTime(TimeOnly.MinValue) });
        command.Parameters.Add(new SqlParameter("@Time", SqlDbType.Time) { Scale = 0, Value = row.Time.ToTimeSpan() });
        command.Parameters.Add(new SqlParameter("@Weekdays", SqlDbType.TinyInt) { Value = row.Weekdays });
        command.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, 200) { Value = row.Title });
        command.Parameters.Add(new SqlParameter("@Note", SqlDbType.NVarChar, 1000) { Value = row.Note });
        command.Parameters.Add(new SqlParameter("@Link", SqlDbType.NVarChar, 2000) { Value = row.Link });
        command.Parameters.Add(new SqlParameter("@IsSpeak", SqlDbType.Bit) { Value = row.IsSpeak });
    }
}
