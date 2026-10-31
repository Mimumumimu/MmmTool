using System.Data;
using Microsoft.Data.SqlClient;
using MmmBatch.NoteSending.Core;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Notes;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Notes.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>
/// 他の人の作成・更新の通知の元になるノートと、通知する相手を、SQL Server から読む。読むだけで、書かない。
/// </summary>
/// <remarks>
/// 更新されたノートの読み方は、MmmTool (PC の通知ダイアログ)と共通の <see cref="SqlServerNoteChangeReader"/>。
/// 通知する相手は、「ノートの通知を受け取る」がオンの人のうち、受け取り先が指定されていて、その送信先が削除されておらず、管理者がノートを無効にしていない人。
/// オンの人は、<c>dbo.AppUserSetting</c> の索引 <c>IX_AppUserSetting_FeatureNo_CategoryNo_CodeNo_Value_UserId</c> で探す。送信先は <c>dbo.NotificationChannel</c> から読む。
/// 全員を一度に読まないよう、人の番号の順に、指定の人数ずつ読む。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerNoteSendSource(SqlServerDatabase database) : INoteSendSource
{
    /// <inheritdoc />
    public Task<NoteChanges> GetChangesAsync(DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken = default)
        => new SqlServerNoteChangeReader(database).ReadAsync(since, today, cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsNoticeTargetAsync(int noteId, DateOnly today, CancellationToken cancellationToken = default)
        => new SqlServerNoteChangeReader(database).IsNoticeTargetAsync(noteId, today, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<NoteRecipient>> GetRecipientsAsync(int afterUserId, int excludeUserId, int count, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<IReadOnlyList<NoteRecipient>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT TOP (@Count) t.UserId, ISNULL(u.DisplayName, N''), c.Id, c.IsDeleted, c.UserId, c.Kind, c.Name, c.Value
                FROM dbo.AppUserSetting AS t
                INNER JOIN dbo.AppUserSetting AS s
                    ON s.UserId = t.UserId AND s.FeatureNo = {ReceiveTarget.FeatureNo} AND s.CategoryNo = {ReceiveTarget.CategoryNo}
                        AND s.CodeNo = {ReceiveTarget.CodeNo} AND s.IsDeleted = 0
                INNER JOIN dbo.NotificationChannel AS c ON c.Id = TRY_CAST(s.Value AS int) AND c.IsDeleted = 0
                LEFT JOIN dbo.AppUser AS u ON u.Id = t.UserId
                WHERE t.FeatureNo = {CodeFeatures.Notes} AND t.CategoryNo = {NotificationFeatureSettings.CategoryNo} AND t.CodeNo = {NotificationFeatureSettings.CodeNo}
                    AND t.Value = N'{NotificationFeatureSettings.On}' AND t.IsDeleted = 0
                    AND t.UserId > @After AND t.UserId <> @Exclude
                    AND NOT {FeatureAccessSql.IsDisabled("t.UserId", CodeFeatures.Notes)}
                ORDER BY t.UserId
                """;
            command.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = count });
            command.Parameters.Add(new SqlParameter("@After", SqlDbType.Int) { Value = afterUserId });
            command.Parameters.Add(new SqlParameter("@Exclude", SqlDbType.Int) { Value = excludeUserId });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var recipients = new List<NoteRecipient>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var channel = new NotificationChannelRow
                {
                    Id = reader.GetInt32(2),
                    IsDeleted = reader.GetBoolean(3),
                    UserId = reader.GetInt32(4),
                    Kind = reader.GetByte(5),
                    Name = reader.GetString(6),
                    Value = reader.GetString(7),
                }.ToChannel();
                recipients.Add(new NoteRecipient(reader.GetInt32(0), reader.GetString(1), channel));
            }
            return recipients;
        }, cancellationToken);
}
