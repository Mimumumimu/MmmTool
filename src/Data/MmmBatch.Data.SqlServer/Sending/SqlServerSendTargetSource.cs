using MmmBatch.Sending.Core;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>
/// 送る対象 (送信先を選んだリマインダーと、その送信先)を、SQL Server から読む。
/// </summary>
/// <remarks>
/// 読むだけで、書かない。リマインダーの宛先・作成者・対応状態は見ない (送信は、登録した人が選んだ送信先へ送るため)。
/// 削除されたリマインダー・送信先・送信設定は含めない。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerSendTargetSource(SqlServerDatabase database) : ISendTargetSource
{
    /// <inheritdoc />
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<IReadOnlyList<SendTarget>> GetTargetsAsync(CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<IReadOnlyList<SendTarget>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT r.Id, r.IsDeleted, r.CreatedByUserId, r.TargetUserId, r.Date, r.Time, r.Weekdays, r.Title, r.Note, r.Link, r.IsSpeak,
                       c.Id, c.IsDeleted, c.CreatedByUserId, c.Kind, c.Name, c.Value, s.UpdatedAt, ISNULL(u.DisplayName, N'')
                FROM dbo.ReminderSendSetting AS s
                INNER JOIN dbo.Reminder AS r ON r.Id = s.ReminderId AND r.IsDeleted = 0
                INNER JOIN dbo.NotificationChannel AS c ON c.Id = s.ChannelId AND c.IsDeleted = 0
                LEFT JOIN dbo.AppUser AS u ON u.Id = c.CreatedByUserId
                WHERE s.IsDeleted = 0
                ORDER BY r.Id
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var targets = new List<SendTarget>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var reminder = new ReminderRow
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
                }.ToReminder();
                var channel = new NotificationChannelRow
                {
                    Id = reader.GetInt32(11),
                    IsDeleted = reader.GetBoolean(12),
                    CreatedByUserId = reader.GetInt32(13),
                    Kind = reader.GetByte(14),
                    Name = reader.GetString(15),
                    Value = reader.GetString(16),
                }.ToChannel();
                targets.Add(new SendTarget(reminder, channel, reader.GetDateTimeOffset(17), reader.GetString(18)));
            }
            return targets;
        }, cancellationToken);
}
