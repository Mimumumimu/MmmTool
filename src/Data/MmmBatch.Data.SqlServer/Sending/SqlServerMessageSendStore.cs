using System.Data;
using Microsoft.Data.SqlClient;
using MmmBatch.MessageSending.Core;
using MmmTool.Data.Messages;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Messages.Core;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>
/// メッセージの送信依頼を、SQL Server の <c>dbo.MessageSendRequest</c> から読み、状態を書く。
/// </summary>
/// <remarks>
/// 行を足すのは MmmTool。ここは読んで、状態を変えるだけ。読むたびに DB から読む。
/// MmmBatch はユーザーではないので、更新者は 0。更新日時は DB サーバーの時計で書く (複数の MmmBatch の時計のずれを入れない)。
/// 送る先 (<c>dbo.NotificationChannel</c>)の値は、ここで読む (送るため)。送信先の行は、論理削除だけで消えないので、内部結合で足りる。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerMessageSendStore(SqlServerDatabase database) : IMessageSendStore
{
    /// <summary>読む列 (<see cref="ReadTarget"/> の並び)</summary>
    private const string Select =
        """
        SELECT r.Id, r.IsDeleted, r.CreatedAt, r.CreatedByUserId, r.UpdatedAt, r.UpdatedByUserId, r.SenderUserId, r.ChannelId, r.Title, r.Body, r.Status, r.Attempts, r.SentAt,
               c.Id, c.IsDeleted, c.UserId, c.Kind, c.Name, c.Value, ISNULL(cu.DisplayName, N''), ISNULL(su.DisplayName, N'')
        FROM dbo.MessageSendRequest AS r
        INNER JOIN dbo.NotificationChannel AS c ON c.Id = r.ChannelId
        LEFT JOIN dbo.AppUser AS cu ON cu.Id = c.UserId
        LEFT JOIN dbo.AppUser AS su ON su.Id = r.SenderUserId
        """;

    /// <inheritdoc />
    public Task<IReadOnlyList<MessageSendTarget>> GetSendableAsync(int maxAttempts, CancellationToken cancellationToken = default)
        => ReadAsync(
            "WHERE r.IsDeleted = 0 AND r.Status IN (@Pending, @Failed) AND r.Attempts < @MaxAttempts ORDER BY r.Id",
            maxAttempts, null, cancellationToken);

    /// <inheritdoc />
    /// <remarks>主キー (<c>Id</c>)の降順で先頭から読む (日付・状態では絞らないので、索引は要らない)。</remarks>
    public Task<IReadOnlyList<MessageSendTarget>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => ReadAsync(
            "WHERE r.IsDeleted = 0 ORDER BY r.Id DESC OFFSET 0 ROWS FETCH NEXT @Count ROWS ONLY",
            0, count, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryClaimAsync(MessageSendRequest current, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);

        return SendTableAccess.RunAsync(database, async connection =>
        {
            // 読んだときの状態・更新日時と同じときだけ更新する。別の MmmBatch が先に取っていれば、0 行になる
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.MessageSendRequest
                SET Status = @Pending, Attempts = Attempts + 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
                WHERE Id = @Id AND Status = @Status AND UpdatedAt = @UpdatedAt
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = current.Id });
            command.Parameters.Add(new SqlParameter("@Pending", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Pending });
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
                UPDATE dbo.MessageSendRequest
                SET Status = @Status, SentAt = SYSDATETIMEOFFSET(), UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
                WHERE Id = @Id
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Sent });
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
                UPDATE dbo.MessageSendRequest
                SET Status = @Status, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
                WHERE Id = @Id
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Failed });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

    /// <summary>条件に合う依頼を、送信先と一緒に読む</summary>
    /// <param name="whereAndOrder">WHERE と ORDER BY (パラメーターは <c>@Pending</c>・<c>@Sent</c>・<c>@Failed</c>・<c>@MaxAttempts</c>・<c>@Count</c>)。状態で絞るときは、索引 (<c>Status</c>)で探せるよう、<c>Status</c> の条件から始める</param>
    /// <param name="maxAttempts">送ろうとする回数の上限</param>
    /// <param name="count">読む最大の件数 (<c>@Count</c>)。使わないときは null</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送る対象</returns>
    private Task<IReadOnlyList<MessageSendTarget>> ReadAsync(string whereAndOrder, int maxAttempts, int? count, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync<IReadOnlyList<MessageSendTarget>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"{Select}\n{whereAndOrder}";
            command.Parameters.Add(new SqlParameter("@Pending", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Pending });
            command.Parameters.Add(new SqlParameter("@Sent", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Sent });
            command.Parameters.Add(new SqlParameter("@Failed", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Failed });
            command.Parameters.Add(new SqlParameter("@MaxAttempts", SqlDbType.Int) { Value = maxAttempts });
            if (count is { } limit)
            {
                command.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = limit });
            }
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var targets = new List<MessageSendTarget>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                targets.Add(ReadTarget(reader));
            }
            return targets;
        }, cancellationToken);

    /// <summary>読んでいる行を、<see cref="Select"/> の並びの <see cref="MessageSendTarget"/> にする</summary>
    /// <param name="reader">読んでいる位置のリーダー</param>
    /// <returns>送る対象</returns>
    /// <exception cref="InvalidOperationException">状態・区分が、定義にない値 (バグか、手で直した値)。</exception>
    private static MessageSendTarget ReadTarget(SqlDataReader reader)
    {
        var request = new MessageSendRequestRow
        {
            Id = reader.GetInt32(0),
            IsDeleted = reader.GetBoolean(1),
            CreatedAt = reader.GetDateTimeOffset(2),
            CreatedByUserId = reader.GetInt32(3),
            UpdatedAt = reader.GetDateTimeOffset(4),
            UpdatedByUserId = reader.GetInt32(5),
            SenderUserId = reader.GetInt32(6),
            ChannelId = reader.GetInt32(7),
            Title = reader.GetString(8),
            Body = reader.GetString(9),
            Status = reader.GetByte(10),
            Attempts = reader.GetByte(11),
            SentAt = reader.GetDateTimeOffset(12),
        }.ToRequest();
        var channel = new NotificationChannelRow
        {
            Id = reader.GetInt32(13),
            IsDeleted = reader.GetBoolean(14),
            UserId = reader.GetInt32(15),
            Kind = reader.GetByte(16),
            Name = reader.GetString(17),
            Value = reader.GetString(18),
        }.ToChannel();
        return new MessageSendTarget(request, channel, reader.GetString(19), reader.GetString(20));
    }
}
