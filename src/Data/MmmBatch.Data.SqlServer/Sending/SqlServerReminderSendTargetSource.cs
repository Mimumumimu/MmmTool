using System.Data;
using Microsoft.Data.SqlClient;
using MmmBatch.ReminderSending.Core;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Users;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>
/// 送る対象 (送信先を選んだリマインダーと、その送信先)を、SQL Server から読む。
/// </summary>
/// <remarks>
/// 読むだけで、書かない。リマインダーの宛先・作成者・対応状態は見ない (送信は、登録した人が選んだ送信先へ送るため)。
/// 削除されたリマインダー・送信先・送信設定は含めない。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerReminderSendTargetSource(SqlServerDatabase database) : IReminderSendTargetSource
{
    /// <inheritdoc />
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    /// <remarks>
    /// 今日と昨日の送信の状況は、状況の表を外部結合して、同じ問い合わせで読む。
    /// 状況の表は <c>(ReminderId, ChannelId, Date)</c> が一意なので、結合しても対象の行は増えない。
    /// </remarks>
    public Task<IReadOnlyList<ReminderSendTargetState>> GetTargetsAsync(DateOnly today, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<IReadOnlyList<ReminderSendTargetState>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            // 送信先の持ち主が、管理者にリマインダーを無効にされているときは、その送信先へ送らない
            command.CommandText =
                $"""
                SELECT r.Id, r.IsDeleted, r.AuthorUserId, r.TargetUserId, r.Date, r.Time, r.Weekdays, r.Title, r.Note, r.Link, r.IsSpeak,
                       c.Id, c.IsDeleted, c.UserId, c.Kind, c.Name, c.Value, s.UpdatedAt, ISNULL(u.DisplayName, N''),
                       {SqlServerReminderSendStatusRepository.ColumnsOf("t")},
                       {SqlServerReminderSendStatusRepository.ColumnsOf("y")}
                FROM dbo.ReminderSendSetting AS s
                INNER JOIN dbo.Reminder AS r ON r.Id = s.ReminderId AND r.IsDeleted = 0
                INNER JOIN dbo.NotificationChannel AS c ON c.Id = s.ChannelId AND c.IsDeleted = 0
                LEFT JOIN dbo.AppUser AS u ON u.Id = c.UserId
                LEFT JOIN dbo.ReminderSendStatus AS t ON t.ReminderId = s.ReminderId AND t.ChannelId = s.ChannelId AND t.Date = @Today
                LEFT JOIN dbo.ReminderSendStatus AS y ON y.ReminderId = s.ReminderId AND y.ChannelId = s.ChannelId AND y.Date = @Yesterday
                WHERE s.IsDeleted = 0 AND NOT {FeatureAccessSql.IsDisabled("c.UserId", CodeFeatures.Reminders)}
                ORDER BY r.Id
                """;
            command.Parameters.Add(new SqlParameter("@Today", SqlDbType.Date) { Value = today.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@Yesterday", SqlDbType.Date) { Value = today.AddDays(-1).ToDateTime(TimeOnly.MinValue) });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var targets = new List<ReminderSendTargetState>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var reminder = new ReminderRow
                {
                    Id = reader.GetInt32(0),
                    IsDeleted = reader.GetBoolean(1),
                    AuthorUserId = reader.GetInt32(2),
                    TargetUserId = reader.GetInt32(3),
                    Date = DateOnly.FromDateTime(reader.GetDateTime(4)),
                    Time = TimeOnly.FromTimeSpan(reader.GetTimeSpan(5)),
                    Weekdays = reader.GetByte(6),
                    Title = reader.GetString(7),
                    Note = reader.GetString(8),
                    Link = reader.GetString(9),
                    IsSpeak = reader.GetBoolean(10),
                }.ToReminder();
                var channel = new NotificationChannelRow
                {
                    Id = reader.GetInt32(11),
                    IsDeleted = reader.GetBoolean(12),
                    UserId = reader.GetInt32(13),
                    Kind = reader.GetByte(14),
                    Name = reader.GetString(15),
                    Value = reader.GetString(16),
                }.ToChannel();
                targets.Add(new ReminderSendTargetState(
                    new ReminderSendTarget(reminder, channel, reader.GetDateTimeOffset(17), reader.GetString(18)),
                    SqlServerReminderSendStatusRepository.ReadStatusOrNull(reader, 19),
                    SqlServerReminderSendStatusRepository.ReadStatusOrNull(reader, 27)));
            }
            return targets;
        }, cancellationToken);
}
