using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Reminders.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>リマインダーの送信設定を SQL Server の dbo.ReminderSendSetting から読む</summary>
/// <remarks>
/// 読むたびに DB から読む。
/// 今のユーザーは <see cref="CurrentUser"/>。
/// 特定していないと読めない。
/// 1 つのリマインダーに送信先を複数持てる。
/// 1 つの送信先につき 1 行。
/// 読むのは自分が使用者の送信先 (<c>NotificationChannel.UserId</c>)の分だけで、ほかの人の送信先の設定は読まない。
/// 書き込みはここではしない。
/// リマインダー本体と同じトランザクションで書くため、本体の保存先 (<see cref="SqlServerReminderRepository"/>・<see cref="SqlServerReminderWebRepository"/>)が <see cref="ReminderSendSettingSql"/> で書く。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
public sealed class SqlServerReminderSendSettingRepository(SqlServerDatabase database, CurrentUser currentUser) : IReminderSendSettingRepository
{
    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
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

    /// <summary>1 件のリマインダーに選んでいる送信先の番号を読む</summary>
    /// <param name="reminderNo">リマインダーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 自分が使用者の送信先の番号
    /// 選んでいない (または削除済み)ときは空
    /// </returns>
    /// <remarks>
    /// Web 版が編集のために 1 件だけ読む
    /// 全リマインダー分を読む <see cref="GetChannelIdsAsync"/> は PC 版の一覧が使う
    /// </remarks>
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<IReadOnlyList<int>> GetChannelIdsOfAsync(int reminderNo, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync<IReadOnlyList<int>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT s.ChannelId
                FROM dbo.ReminderSendSetting AS s
                INNER JOIN dbo.NotificationChannel AS c ON c.Id = s.ChannelId
                WHERE s.ReminderId = @ReminderId AND s.IsDeleted = 0 AND c.UserId = @UserId
                ORDER BY s.ChannelId
                """;
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@ReminderId", SqlDbType.Int) { Value = reminderNo });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var channelIds = new List<int>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                channelIds.Add(reader.GetInt32(0));
            }
            return channelIds;
        }, cancellationToken);
    }

}
