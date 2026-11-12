using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Reminders.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>Web 版のリマインダーを SQL Server の dbo.Reminder・dbo.ReminderState から、要求ごとに読み、書く</summary>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
/// <remarks>
/// <para>
/// PC 版の <see cref="SqlServerReminderRepository"/> は全件をメモリに持って毎分読み直す、アプリ全体で 1 つの保存先。
/// Web 版は要求ごとに作り、メモリに持たない。
/// 今日の対象は日付と曜日の条件を問い合わせの中で絞って読む。
/// 自分の対応状態は同じ問い合わせで外部結合して読む。
/// 対応状態は読むだけで、書かない。
/// </para>
/// <para>
/// 見える範囲 (宛先が自分・全員宛て・自分が作成したもの)と、書き込みの SQL は PC 版と同じ <see cref="ReminderSql"/>。
/// 共通ヘッダーの列 (<c>CreatedByUserId</c> など)は条件に使わない。
/// 完全削除はしない。
/// </para>
/// </remarks>
public sealed class SqlServerReminderWebRepository(SqlServerDatabase database, CurrentUser currentUser)
{
    /// <summary>曜日指定を表す日付</summary>
    private static readonly DateTime NoDate = DateOnly.MaxValue.ToDateTime(TimeOnly.MinValue);

    /// <summary>ある日の対象を読む問い合わせ</summary>
    /// <remarks>ReminderSql.ReadReminder の並びにその日の対応状態が続く</remarks>
    private static readonly string TodaySql =
        $"""
        SELECT {ReminderSql.PrefixedColumns("r")}, ISNULL(s.Status, 0)
        FROM dbo.Reminder AS r
        LEFT JOIN dbo.ReminderState AS s
            ON s.ReminderId = r.Id AND s.UserId = @UserId AND s.IsDeleted = 0 AND s.Date = @Day
        WHERE r.IsDeleted = 0 AND r.TargetUserId IN (0, @UserId)
          AND (r.Date = @Day OR (r.Date = @NoDate AND (r.Weekdays IN (0, 127) OR (r.Weekdays & @WeekdayBit) <> 0)))
        ORDER BY r.Time, r.Id
        """;

    /// <summary>一覧を読む問い合わせ</summary>
    /// <remarks>日付 → 時刻 → 番号の順で、曜日指定は後ろに並ぶ。</remarks>
    private static readonly string ListSql =
        $"""
        {ReminderSql.Select}
        WHERE {ReminderSql.VisibleCondition}
          AND (@IncludeDeleted = 1 OR IsDeleted = 0)
          AND (@IncludePast = 1 OR Date = @NoDate OR Date >= @Today)
        ORDER BY Date, Time, Id
        """;

    /// <summary>今のユーザーの番号</summary>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない
    /// ログインが必要
    /// </exception>
    private int UserId
    {
        get
        {
            const string Message = "ユーザーが特定されていないため、リマインダーを使えません。ログインが必要です。";
            return currentUser.User?.Id ?? throw new DataFileException(Message, new InvalidOperationException(Message));
        }
    }

    /// <summary>今のユーザーの番号</summary>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない
    /// ログインが必要
    /// </exception>
    public int CurrentUserId => UserId;

    /// <summary>ある日の対象とその日の対応状態を読む</summary>
    /// <param name="day">日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// その日に発動する、削除されていない、宛先が自分と全員宛てのリマインダーで、時刻、番号の順。
    /// 時刻が正しくないものは外す。
    /// </returns>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・接続できない・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<IReadOnlyList<ReminderTarget>> GetTodayAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        return database.RunAsync<IReadOnlyList<ReminderTarget>>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = TodaySql;
            ReminderSql.AddUserParameter(command, userId);
            command.Parameters.Add(new SqlParameter("@Day", SqlDbType.Date) { Value = day.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@NoDate", SqlDbType.Date) { Value = NoDate });
            command.Parameters.Add(new SqlParameter("@WeekdayBit", SqlDbType.TinyInt) { Value = WeekdayBit(day) });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var targets = new List<ReminderTarget>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var reminder = ReminderSql.ReadReminder(reader);
                if (ReminderDates.ToTime(reminder.Time) is not null)
                {
                    targets.Add(new ReminderTarget(reminder, (ReminderStatus)reader.GetByte(11)));
                }
            }
            return targets;
        }, cancellationToken);
    }

    /// <summary>見えるリマインダーの一覧を読む</summary>
    /// <param name="today">
    /// 今日
    /// 「過去の予定」を外す基準
    /// </param>
    /// <param name="includePast">
    /// 日付が過去の日付指定も含めるか
    /// 今日は含む
    /// </param>
    /// <param name="includeDeleted">削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 日付、時刻、番号の順の一覧。
    /// 曜日指定は後ろ。
    /// </returns>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・接続できない・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<IReadOnlyList<Reminder>> GetListAsync(DateOnly today, bool includePast, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        return database.RunAsync<IReadOnlyList<Reminder>>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ListSql;
            ReminderSql.AddUserParameter(command, userId);
            command.Parameters.Add(new SqlParameter("@NoDate", SqlDbType.Date) { Value = NoDate });
            command.Parameters.Add(new SqlParameter("@Today", SqlDbType.Date) { Value = today.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@IncludePast", SqlDbType.Bit) { Value = includePast });
            command.Parameters.Add(new SqlParameter("@IncludeDeleted", SqlDbType.Bit) { Value = includeDeleted });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var reminders = new List<Reminder>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                reminders.Add(ReminderSql.ReadReminder(reader));
            }
            return reminders;
        }, cancellationToken);
    }

    /// <summary>番号を指定して見えるリマインダーを 1 件読む</summary>
    /// <param name="no">リマインダーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 削除済みも含めて返す
    /// 無い・見えないときは null
    /// </returns>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・接続できない・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<Reminder?> FindAsync(int no, CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        return database.RunAsync<Reminder?>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"{ReminderSql.Select} WHERE Id = @Id AND {ReminderSql.VisibleCondition}";
            ReminderSql.AddUserParameter(command, userId);
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = no });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReminderSql.ReadReminder(reader) : null;
        }, cancellationToken);
    }

    /// <summary>通知の記録から、見えて削除されていないリマインダーの番号と送った日を 1 回で読む</summary>
    /// <param name="sendStatusId">送信の状況の番号 (<c>ReminderSendStatus.Id</c>)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 自分の送信先に届いた記録で、リマインダーが見えて削除されていないときだけ、リマインダーの番号と送った日。
    /// それ以外は null。
    /// </returns>
    /// <remarks>
    /// 送信先の使用者 (<c>NotificationChannel.UserId</c>)とリマインダーの見える範囲で絞る
    /// 共通ヘッダーの列は条件に使わない
    /// </remarks>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・接続できない・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<(int ReminderNo, DateOnly Date)?> FindNotifiedAsync(int sendStatusId, CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        return database.RunAsync<(int, DateOnly)?>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT s.ReminderId, s.Date
                FROM dbo.ReminderSendStatus AS s
                INNER JOIN dbo.NotificationChannel AS c ON c.Id = s.ChannelId AND c.UserId = @UserId
                INNER JOIN dbo.Reminder AS r ON r.Id = s.ReminderId
                WHERE s.Id = @Id AND r.IsDeleted = 0 AND (r.TargetUserId IN (0, @UserId) OR r.AuthorUserId = @UserId)
                """;
            ReminderSql.AddUserParameter(command, userId);
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = sendStatusId });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? (reader.GetInt32(0), DateOnly.FromDateTime(reader.GetDateTime(1)))
                : null;
        }, cancellationToken);
    }

    /// <summary>リマインダーを追加する</summary>
    /// <param name="reminder">追加するリマインダー (番号は無視する)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>追加した内容 (採番した番号と作成者つき)</returns>
    /// <exception cref="ArgumentException">日付・時刻・曜日・宛先・文字数が DB に入れられない値</exception>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・管理者が無効にしている・接続できない・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    public async Task<Reminder> AddAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        var row = ReminderRow.FromReminder(reminder with { No = 0, IsDeleted = false });
        var userId = UserId;
        var id = await WriteAsync(
            (connection, transaction) => ReminderSql.InsertAsync(connection, transaction, userId, row, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return (row with { Id = id, AuthorUserId = userId }).ToReminder();
    }

    /// <summary>既存のリマインダーを同じ番号の内容で置き換える</summary>
    /// <param name="reminder">置き換える内容 (番号で対象を決める)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 見えて更新できたとき true
    /// 無い・見えないとき false
    /// </returns>
    /// <remarks>削除済みを編集すると削除が取り消される</remarks>
    /// <exception cref="ArgumentException">日付・時刻・曜日・宛先・文字数が DB に入れられない値</exception>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・管理者が無効にしている・接続できない・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<bool> UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        var row = ReminderRow.FromReminder(reminder with { IsDeleted = false });
        var userId = UserId;
        return WriteAsync(
            (connection, transaction) => ReminderSql.UpdateAsync(connection, transaction, userId, row, cancellationToken),
            cancellationToken);
    }

    /// <summary>リマインダーの削除フラグを変える</summary>
    /// <param name="no">リマインダーの番号</param>
    /// <param name="isDeleted">削除済みにするか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 見えて書けたとき true
    /// 無い・見えないとき false
    /// </returns>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・接続できない・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<bool> SetDeletedAsync(int no, bool isDeleted, CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        return WriteAsync(
            (connection, transaction) => ReminderSql.SetDeletedAsync(connection, transaction, userId, no, isDeleted, cancellationToken),
            cancellationToken);
    }

    /// <summary>トランザクションで書き込む</summary>
    /// <typeparam name="T">書き込みの結果の型</typeparam>
    /// <param name="work">開いた接続とトランザクションで行う書き込み</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの結果</returns>
    private Task<T> WriteAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> work, CancellationToken cancellationToken)
        => database.RunAsync(async connection =>
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var value = await work(connection, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return value;
        }, cancellationToken);

    /// <summary>日の曜日を曜日指定の曜日の 1 つのビットにする</summary>
    /// <param name="day">日</param>
    /// <returns>その曜日のビット</returns>
    /// <remarks>月 = 1・火 = 2 … 日 = 64</remarks>
    private static byte WeekdayBit(DateOnly day) => (byte)(1 << (((int)day.DayOfWeek + 6) % 7));
}
