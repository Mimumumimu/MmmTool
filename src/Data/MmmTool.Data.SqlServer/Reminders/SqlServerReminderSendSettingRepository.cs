using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Reminders.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>
/// リマインダーの送信設定を、SQL Server の <c>dbo.ReminderSendSetting</c> に保存する。
/// </summary>
/// <remarks>
/// 読むたびに DB から読む。操作した人 (作成者・更新者)は <see cref="CurrentUser"/>。特定していないと、保存できない。
/// 1 つのリマインダーに、送信先を複数持てる (1 つの送信先につき 1 行)。選んでいない送信先の行は論理削除する。
/// 選び直したときは、そのリマインダーと送信先の既存の行 (削除済みも含む)を更新して使い回し、無いときだけ追加する。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
public sealed class SqlServerReminderSendSettingRepository(SqlServerDatabase database, CurrentUser currentUser) : IReminderSendSettingRepository
{
    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetChannelIdsAsync(CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<IReadOnlyDictionary<int, IReadOnlyList<int>>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT ReminderId, ChannelId FROM dbo.ReminderSendSetting WHERE IsDeleted = 0 ORDER BY ReminderId, ChannelId";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var settings = new Dictionary<int, List<int>>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var reminderId = reader.GetInt32(0);
                if (!settings.TryGetValue(reminderId, out var channels))
                {
                    settings[reminderId] = channels = [];
                }
                channels.Add(reader.GetInt32(1));
            }
            return settings.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value);
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task SetChannelsAsync(int reminderNo, IReadOnlyCollection<int> channelIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channelIds);

        var userId = SendTableAccess.RequireUserId(currentUser);
        var wanted = channelIds.Where(id => id > 0).Distinct().ToList();
        return SendTableAccess.RunAsync(database, async connection =>
        {
            // 途中で失敗して、一部だけが書き換わらないよう、まとめて 1 つのトランザクションにする
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await ClearUnselectedAsync(connection, transaction, reminderNo, wanted, userId, cancellationToken).ConfigureAwait(false);
            foreach (var channelId in wanted)
            {
                await UpsertAsync(connection, transaction, reminderNo, channelId, userId, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    /// <summary>選んでいない送信先の設定を、論理削除する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション</param>
    /// <param name="reminderNo">リマインダーの番号</param>
    /// <param name="wanted">選んだ送信先の番号</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>論理削除の完了を表すタスク</returns>
    private static async Task ClearUnselectedAsync(
        SqlConnection connection, SqlTransaction transaction, int reminderNo, IReadOnlyList<int> wanted, int userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // 番号は整数なので、そのまま並べても、SQL に文字列を差し込むことにならない
        var exclusion = wanted.Count == 0 ? "" : $" AND ChannelId NOT IN ({string.Join(',', wanted)})";
        command.CommandText =
            $"""
            UPDATE dbo.ReminderSendSetting
            SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
            WHERE ReminderId = @ReminderId AND IsDeleted = 0{exclusion}
            """;
        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
        command.Parameters.Add(new SqlParameter("@ReminderId", SqlDbType.Int) { Value = reminderNo });
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>選んだ送信先の設定を、更新日時を今にして、削除されていない行にする (無ければ追加する)</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション</param>
    /// <param name="reminderNo">リマインダーの番号</param>
    /// <param name="channelId">送信先の番号</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新または追加の完了を表すタスク</returns>
    private static async Task UpsertAsync(
        SqlConnection connection, SqlTransaction transaction, int reminderNo, int channelId, int userId, CancellationToken cancellationToken)
    {
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE dbo.ReminderSendSetting
                SET IsDeleted = 0, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = (SELECT TOP (1) Id FROM dbo.ReminderSendSetting WHERE ReminderId = @ReminderId AND ChannelId = @ChannelId ORDER BY IsDeleted, Id DESC)
                """;
            update.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            update.Parameters.Add(new SqlParameter("@ReminderId", SqlDbType.Int) { Value = reminderNo });
            update.Parameters.Add(new SqlParameter("@ChannelId", SqlDbType.Int) { Value = channelId });
            if (await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1)
            {
                return;
            }
        }

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO dbo.ReminderSendSetting (CreatedByUserId, UpdatedByUserId, ReminderId, ChannelId)
            VALUES (@UserId, @UserId, @ReminderId, @ChannelId)
            """;
        insert.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
        insert.Parameters.Add(new SqlParameter("@ReminderId", SqlDbType.Int) { Value = reminderNo });
        insert.Parameters.Add(new SqlParameter("@ChannelId", SqlDbType.Int) { Value = channelId });
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
