using System.Data;
using Microsoft.Data.SqlClient;
using MmmBatch.Sending.Core;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Reminders.Core;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>
/// リマインダーの送信の状況 (<see cref="ReminderSendStatus"/>)を、SQL Server の <c>dbo.ReminderSendStatus</c> に保存する。
/// </summary>
/// <remarks>
/// 読むたびに DB から読む。MmmBatch はユーザーではないので、作成者・更新者は 0。更新日時は DB サーバーの時計で書く (複数の MmmBatch の時計のずれを入れない)。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerReminderSendStatusRepository(SqlServerDatabase database) : IReminderSendStatusRepository
{
    /// <summary>失敗の理由の最大の長さ (列の長さ)</summary>
    private const int LastErrorMaxLength = 500;

    /// <summary>読む列 (<see cref="ReadStatus"/> の並び)</summary>
    private const string Columns = "Id, ReminderId, ChannelId, Date, Status, Attempts, LastError, SentAt, UpdatedAt";

    /// <summary>まだ送っていないことを表す日時 (<c>9999-12-31</c>)</summary>
    private static readonly DateTimeOffset NotSent = new(DateTime.MaxValue.Date, TimeSpan.Zero);

    /// <inheritdoc />
    public Task<IReadOnlyList<ReminderSendStatus>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<IReadOnlyList<ReminderSendStatus>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM dbo.ReminderSendStatus WHERE Date = @Date";
            command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = date.ToDateTime(TimeOnly.MinValue) });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var statuses = new List<ReminderSendStatus>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                statuses.Add(ReadStatus(reader));
            }
            return statuses;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<ReminderSendStatus?> TryCreateAsync(int reminderId, int channelId, DateOnly date, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<ReminderSendStatus?>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO dbo.ReminderSendStatus (CreatedByUserId, UpdatedByUserId, ReminderId, ChannelId, Date, Status, Attempts)
                OUTPUT INSERTED.Id, INSERTED.UpdatedAt
                VALUES (0, 0, @ReminderId, @ChannelId, @Date, @Status, 1)
                """;
            command.Parameters.Add(new SqlParameter("@ReminderId", SqlDbType.Int) { Value = reminderId });
            command.Parameters.Add(new SqlParameter("@ChannelId", SqlDbType.Int) { Value = channelId });
            command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = date.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)SendStatus.Pending });

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                return new ReminderSendStatus(reader.GetInt32(0), reminderId, channelId, date, SendStatus.Pending, 1, "", null, reader.GetDateTimeOffset(1));
            }
            catch (SqlException ex) when (SendTableAccess.DuplicateKeyErrors.Contains(ex.Number))
            {
                // 別の MmmBatch が、先に作った
                return null;
            }
        }, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryClaimRetryAsync(ReminderSendStatus current, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);

        return SendTableAccess.RunAsync(database, async connection =>
        {
            // 読んだときの状態・更新日時と同じときだけ更新する。別の MmmBatch が先に取っていれば、0 行になる
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.ReminderSendStatus
                SET Status = @Pending, Attempts = Attempts + 1, LastError = N'', UpdatedAt = SYSDATETIMEOFFSET()
                WHERE Id = @Id AND Status = @Status AND UpdatedAt = @UpdatedAt
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = current.Id });
            command.Parameters.Add(new SqlParameter("@Pending", SqlDbType.TinyInt) { Value = (byte)SendStatus.Pending });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)current.Status });
            command.Parameters.Add(new SqlParameter("@UpdatedAt", SqlDbType.DateTimeOffset, 0) { Value = current.UpdatedAt, Scale = 3 });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task MarkSentAsync(int id, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.ReminderSendStatus
                SET Status = @Status, LastError = N'', SentAt = SYSDATETIMEOFFSET(), UpdatedAt = SYSDATETIMEOFFSET()
                WHERE Id = @Id
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)SendStatus.Sent });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

    /// <inheritdoc />
    public Task MarkFailedAsync(int id, string error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        var message = error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;
        return SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.ReminderSendStatus
                SET Status = @Status, LastError = @LastError, UpdatedAt = SYSDATETIMEOFFSET()
                WHERE Id = @Id
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)SendStatus.Failed });
            command.Parameters.Add(new SqlParameter("@LastError", SqlDbType.NVarChar, LastErrorMaxLength) { Value = message });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SendHistoryItem>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<IReadOnlyList<SendHistoryItem>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT TOP (@Count) st.Id, st.ReminderId, st.ChannelId, st.Date, st.Status, st.Attempts, st.LastError, st.SentAt, st.UpdatedAt,
                       ISNULL(r.Title, N''), ISNULL(c.Name, N''), ISNULL(c.Kind, 0), ISNULL(u.DisplayName, N'')
                FROM dbo.ReminderSendStatus AS st
                LEFT JOIN dbo.Reminder AS r ON r.Id = st.ReminderId
                LEFT JOIN dbo.NotificationChannel AS c ON c.Id = st.ChannelId
                LEFT JOIN dbo.AppUser AS u ON u.Id = c.CreatedByUserId
                ORDER BY st.UpdatedAt DESC, st.Id DESC
                """;
            command.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = count });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var items = new List<SendHistoryItem>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var kind = (NotificationChannelKind)reader.GetByte(11);
                items.Add(new SendHistoryItem(ReadStatus(reader), reader.GetString(9), Enum.IsDefined(kind) ? kind : null, reader.GetString(10), reader.GetString(12)));
            }
            return items;
        }, cancellationToken);

    /// <summary>読んでいる行を、<see cref="Columns"/> の並びの <see cref="ReminderSendStatus"/> にする</summary>
    /// <param name="reader">読んでいる位置のリーダー</param>
    /// <returns>送信の状況</returns>
    /// <exception cref="InvalidOperationException">状態が、定義にない値 (バグか、手で直した値)。</exception>
    private static ReminderSendStatus ReadStatus(SqlDataReader reader)
    {
        var status = (SendStatus)reader.GetByte(4);
        if (!Enum.IsDefined(status))
        {
            throw new InvalidOperationException($"送信の状況 {reader.GetInt32(0)} の状態が正しくありません ({(int)status})。");
        }

        var sentAt = reader.GetDateTimeOffset(7);
        return new ReminderSendStatus(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            DateOnly.FromDateTime(reader.GetDateTime(3)),
            status,
            reader.GetByte(5),
            reader.GetString(6),
            sentAt >= NotSent ? null : sentAt,
            reader.GetDateTimeOffset(8));
    }
}
