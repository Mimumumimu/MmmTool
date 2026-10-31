using System.Data;
using Microsoft.Data.SqlClient;
using MmmBatch.ReminderSending.Core;
using MmmBatch.Sending.Core;
using MmmTool.Data.SqlServer.Connection;

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
    /// <summary>読む列 (<see cref="ReadStatus"/> の並び)</summary>
    private const string Columns = "Id, ReminderId, ChannelId, Date, Status, Attempts, SentAt, UpdatedAt";

    /// <summary>まだ送っていないことを表す日時 (<c>9999-12-31</c>)</summary>
    private static readonly DateTimeOffset NotSent = new(DateTime.MaxValue.Date, TimeSpan.Zero);

    /// <inheritdoc />
    public Task<ReminderSendStatus?> GetAsync(int reminderId, int channelId, DateOnly date, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<ReminderSendStatus?>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM dbo.ReminderSendStatus WHERE ReminderId = @ReminderId AND ChannelId = @ChannelId AND Date = @Date";
            command.Parameters.Add(new SqlParameter("@ReminderId", SqlDbType.Int) { Value = reminderId });
            command.Parameters.Add(new SqlParameter("@ChannelId", SqlDbType.Int) { Value = channelId });
            command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = date.ToDateTime(TimeOnly.MinValue) });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadStatus(reader) : null;
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
                return new ReminderSendStatus(reader.GetInt32(0), reminderId, channelId, date, SendStatus.Pending, 1, null, reader.GetDateTimeOffset(1));
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
                SET Status = @Pending, Attempts = Attempts + 1, UpdatedAt = SYSDATETIMEOFFSET()
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
                SET Status = @Status, SentAt = SYSDATETIMEOFFSET(), UpdatedAt = SYSDATETIMEOFFSET()
                WHERE Id = @Id
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)SendStatus.Sent });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

    /// <inheritdoc />
    public Task MarkFailedAsync(int id, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.ReminderSendStatus
                SET Status = @Status, UpdatedAt = SYSDATETIMEOFFSET()
                WHERE Id = @Id
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)SendStatus.Failed });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

    /// <summary>読む列 (<see cref="ReadStatus"/> の並び)に、表の別名を付けたもの</summary>
    /// <param name="alias">表の別名。定数だけを渡す (SQL に埋め込むため)</param>
    /// <returns>別名つきの列の並び (8 列)</returns>
    /// <remarks>送る対象の問い合わせが、状況の表を結合して読むときに使う。</remarks>
    internal static string ColumnsOf(string alias)
        => $"{alias}.Id, {alias}.ReminderId, {alias}.ChannelId, {alias}.Date, {alias}.Status, {alias}.Attempts, {alias}.SentAt, {alias}.UpdatedAt";

    /// <summary>外部結合で読んだ行を、送信の状況にする。結合先の行が無いときは null</summary>
    /// <param name="reader">読んでいる位置のリーダー</param>
    /// <param name="offset">状況の 8 列の、先頭の位置</param>
    /// <returns>送信の状況。結合先の行が無いとき null</returns>
    /// <exception cref="InvalidOperationException">状態が、定義にない値になっている。バグか、手で直した値。</exception>
    internal static ReminderSendStatus? ReadStatusOrNull(SqlDataReader reader, int offset)
        => reader.IsDBNull(offset) ? null : ReadStatus(reader, offset);

    /// <summary>読んでいる行を、<see cref="Columns"/> の並びの <see cref="ReminderSendStatus"/> にする</summary>
    /// <param name="reader">読んでいる位置のリーダー</param>
    /// <param name="offset">状況の 8 列の、先頭の位置</param>
    /// <returns>送信の状況</returns>
    /// <exception cref="InvalidOperationException">状態が、定義にない値になっている。バグか、手で直した値。</exception>
    private static ReminderSendStatus ReadStatus(SqlDataReader reader, int offset = 0)
    {
        var status = (SendStatus)reader.GetByte(offset + 4);
        if (!Enum.IsDefined(status))
        {
            throw new InvalidOperationException($"送信の状況 {reader.GetInt32(offset)} の状態が正しくありません。状態の値は {(int)status} です。");
        }

        var sentAt = reader.GetDateTimeOffset(offset + 6);
        return new ReminderSendStatus(
            reader.GetInt32(offset),
            reader.GetInt32(offset + 1),
            reader.GetInt32(offset + 2),
            DateOnly.FromDateTime(reader.GetDateTime(offset + 3)),
            status,
            reader.GetByte(offset + 5),
            sentAt >= NotSent ? null : sentAt,
            reader.GetDateTimeOffset(offset + 7));
    }
}
