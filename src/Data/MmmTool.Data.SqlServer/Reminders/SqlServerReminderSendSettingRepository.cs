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
/// 読み書きするのは、自分が使用者の送信先 (<c>NotificationChannel.UserId</c>)の分だけ。ほかの人の送信先の設定は、読まず、足さず、外さない
/// (全員宛てのリマインダーに、複数の人が送信先を付けたとき、片方の保存で、ほかの人の設定が消えないようにするため)。
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
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync<IReadOnlyDictionary<int, IReadOnlyList<int>>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT s.ReminderId, s.ChannelId
                FROM dbo.ReminderSendSetting AS s
                INNER JOIN dbo.NotificationChannel AS c ON c.Id = s.ChannelId
                WHERE s.IsDeleted = 0 AND c.UserId = @UserId
                ORDER BY s.ReminderId, s.ChannelId
                """;
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
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
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task SetChannelsAsync(int reminderNo, IReadOnlyCollection<int> channelIds, CancellationToken cancellationToken = default)
        => SetChannelsAsync([reminderNo], channelIds, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 送信先の確認と、不要な設定の論理削除と、選んだ設定の更新と、無いものの追加を、1 つのトランザクションの 1 回の往復で行う。
    /// 番号は JSON の 1 つのパラメーターで渡す。リマインダーや送信先の数が増えても、問い合わせの数は変わらない。
    /// </remarks>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    public Task SetChannelsAsync(IReadOnlyCollection<int> reminderNos, IReadOnlyCollection<int> channelIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminderNos);
        ArgumentNullException.ThrowIfNull(channelIds);

        var userId = SendTableAccess.RequireUserId(currentUser);
        var reminders = reminderNos.Where(no => no > 0).Distinct().ToList();
        if (reminders.Count == 0)
        {
            return Task.CompletedTask;
        }

        var wanted = channelIds.Where(id => id > 0).Distinct().ToList();
        return SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SET XACT_ABORT ON;
                BEGIN TRAN;

                -- 自分が使用者の送信先だけを対象にする。ほかの人の送信先の番号が混ざっても、足さない
                DECLARE @Wanted TABLE (ChannelId int PRIMARY KEY);
                INSERT INTO @Wanted (ChannelId)
                SELECT DISTINCT j.ChannelId FROM OPENJSON(@Channels) WITH (ChannelId int '$') AS j
                WHERE EXISTS (SELECT 1 FROM dbo.NotificationChannel AS c WHERE c.Id = j.ChannelId AND c.UserId = @UserId AND c.IsDeleted = 0);

                DECLARE @Reminders TABLE (ReminderId int PRIMARY KEY);
                INSERT INTO @Reminders (ReminderId)
                SELECT DISTINCT j.ReminderId FROM OPENJSON(@ReminderIds) WITH (ReminderId int '$') AS j;

                -- 選んでいない送信先の設定を、論理削除する。自分が使用者の送信先の分だけ
                UPDATE s SET s.IsDeleted = 1, s.UpdatedAt = SYSDATETIMEOFFSET(), s.UpdatedByUserId = @UserId
                FROM dbo.ReminderSendSetting AS s
                WHERE s.ReminderId IN (SELECT ReminderId FROM @Reminders) AND s.IsDeleted = 0
                  AND s.ChannelId NOT IN (SELECT ChannelId FROM @Wanted)
                  AND s.ChannelId IN (SELECT Id FROM dbo.NotificationChannel WHERE UserId = @UserId);

                -- 選んだ送信先の既存の行 (削除済みも含む)を、更新日時を今にして、削除されていない行にする。削除されていない行を先にして、番号の大きい行を使う
                WITH ranked AS (
                    SELECT s.IsDeleted, s.UpdatedAt, s.UpdatedByUserId,
                           ROW_NUMBER() OVER (PARTITION BY s.ReminderId, s.ChannelId ORDER BY s.IsDeleted, s.Id DESC) AS rn
                    FROM dbo.ReminderSendSetting AS s
                    WHERE s.ReminderId IN (SELECT ReminderId FROM @Reminders) AND s.ChannelId IN (SELECT ChannelId FROM @Wanted))
                UPDATE ranked SET IsDeleted = 0, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId WHERE rn = 1;

                -- 行が無いものだけ、追加する
                INSERT INTO dbo.ReminderSendSetting (CreatedByUserId, UpdatedByUserId, ReminderId, ChannelId)
                SELECT @UserId, @UserId, r.ReminderId, w.ChannelId
                FROM @Reminders AS r CROSS JOIN @Wanted AS w
                WHERE NOT EXISTS (SELECT 1 FROM dbo.ReminderSendSetting AS s WHERE s.ReminderId = r.ReminderId AND s.ChannelId = w.ChannelId);

                COMMIT TRAN;
                """;
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@ReminderIds", SqlDbType.NVarChar, -1) { Value = IdJson.From(reminders) });
            command.Parameters.Add(new SqlParameter("@Channels", SqlDbType.NVarChar, -1) { Value = IdJson.From(wanted) });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }
}
