using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Reminders.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>
/// 送信先 (<see cref="NotificationChannel"/>)を、SQL Server の <c>dbo.NotificationChannel</c> に保存する。
/// </summary>
/// <remarks>
/// 読むたびに DB から読む (件数が少なく、メモリに持たない)。操作した人 (作成者・更新者)は <see cref="CurrentUser"/>。特定していないと、読み込めず、保存もできない。
/// 一覧に出るのは、自分が登録したものだけ。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
public sealed class SqlServerNotificationChannelRepository(SqlServerDatabase database, CurrentUser currentUser) : INotificationChannelRepository
{
    /// <summary>読む列 (<see cref="ReadRow"/> の並び)</summary>
    private const string Columns = "Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, Kind, Name, Value";

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public int CurrentUserId => currentUser.User?.Id ?? 0;

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<IReadOnlyList<NotificationChannel>> GetChannelsAsync(bool includeDeleted, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync<IReadOnlyList<NotificationChannel>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM dbo.NotificationChannel WHERE CreatedByUserId = @UserId"
                + (includeDeleted ? "" : " AND IsDeleted = 0")
                + " ORDER BY Id";
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var channels = new List<NotificationChannel>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                channels.Add(ReadRow(reader).ToChannel());
            }
            return channels;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<NotificationChannel?> FindAsync(int id, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<NotificationChannel?>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM dbo.NotificationChannel WHERE Id = @Id";
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRow(reader).ToChannel() : null;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">登録名・値が空か長すぎる。</exception>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task<NotificationChannel> AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        var row = NotificationChannelRow.FromChannel(channel with { Id = 0, IsDeleted = false });
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO dbo.NotificationChannel (CreatedByUserId, UpdatedByUserId, Kind, Name, Value)
                OUTPUT INSERTED.Id
                VALUES (@UserId, @UserId, @Kind, @Name, @Value)
                """;
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@Kind", SqlDbType.TinyInt) { Value = row.Kind });
            command.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, NotificationChannelRules.NameMaxLength) { Value = row.Name });
            command.Parameters.Add(new SqlParameter("@Value", SqlDbType.NVarChar, NotificationChannelRules.ValueMaxLength) { Value = row.Value });

            var id = (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            return (row with { Id = id, CreatedByUserId = userId, UpdatedByUserId = userId }).ToChannel();
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">登録名・値が空か長すぎる。</exception>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task<bool> UpdateAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        var row = NotificationChannelRow.FromChannel(channel);
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            // 自分が登録したものだけ、区分は変えずに、名前と値を更新する
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.NotificationChannel
                SET Name = @Name, Value = @Value, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @Id AND CreatedByUserId = @UserId AND Kind = @Kind
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = row.Id });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@Kind", SqlDbType.TinyInt) { Value = row.Kind });
            command.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, NotificationChannelRules.NameMaxLength) { Value = row.Name });
            command.Parameters.Add(new SqlParameter("@Value", SqlDbType.NVarChar, NotificationChannelRules.ValueMaxLength) { Value = row.Value });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.NotificationChannel
                SET IsDeleted = @IsDeleted, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @Id AND CreatedByUserId = @UserId
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@IsDeleted", SqlDbType.Bit) { Value = isDeleted });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }, cancellationToken);
    }

    /// <summary>読んでいる行を、<see cref="Columns"/> の並びの <see cref="NotificationChannelRow"/> にする</summary>
    /// <param name="reader">読んでいる位置のリーダー</param>
    /// <returns>DB の行</returns>
    private static NotificationChannelRow ReadRow(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        IsDeleted = reader.GetBoolean(1),
        CreatedAt = reader.GetDateTimeOffset(2),
        CreatedByUserId = reader.GetInt32(3),
        UpdatedAt = reader.GetDateTimeOffset(4),
        UpdatedByUserId = reader.GetInt32(5),
        Kind = reader.GetByte(6),
        Name = reader.GetString(7),
        Value = reader.GetString(8),
    };
}
