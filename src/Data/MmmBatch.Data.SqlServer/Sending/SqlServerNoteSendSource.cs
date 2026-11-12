using System.Data;
using Microsoft.Data.SqlClient;
using MmmBatch.NoteSending.Core;
using MmmTool.CodeMaster.Core;
using MmmTool.Data;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Notes;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Notes.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>他の人の作成・更新の通知の元になるノートと通知する相手を SQL Server から読む</summary>
/// <remarks>
/// 読むだけで、書かない。
/// 更新されたノートの読み方は MmmTool (PC の通知ダイアログ)と共通の <see cref="SqlServerNoteChangeReader"/>。
/// 通知する相手は「ノートの通知を受け取る」がオンの人のうち、通知先が指定されていて、その送信先が削除されておらず、管理者がノートを無効にしていない人。
/// オンの人は <c>dbo.AppUserSetting</c> の索引 <c>IX_AppUserSetting_FeatureNo_CategoryNo_CodeNo_Value_UserId</c> で探す。
/// 送信先は <c>dbo.NotificationChannel</c> から読む。
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
        => ReadRecipientsAsync(
            "TOP (@Count) ",
            "t.UserId > @After AND t.UserId <> @Exclude",
            command =>
            {
                command.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = count });
                command.Parameters.Add(new SqlParameter("@After", SqlDbType.Int) { Value = afterUserId });
                command.Parameters.Add(new SqlParameter("@Exclude", SqlDbType.Int) { Value = excludeUserId });
            },
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 人の番号は JSON の 1 つのパラメーターで渡す
    /// 条件は <see cref="GetRecipientsAsync"/> と同じ問い合わせを共有する
    /// </remarks>
    public Task<IReadOnlyList<NoteRecipient>> GetRecipientsByIdsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<NoteRecipient>>([]);
        }

        return ReadRecipientsAsync(
            "",
            "t.UserId IN (SELECT j.UserId FROM OPENJSON(@UserIds) WITH (UserId int '$') AS j)",
            command => command.Parameters.Add(new SqlParameter("@UserIds", SqlDbType.NVarChar, -1) { Value = IdJson.From(userIds) }),
            cancellationToken);
    }

    /// <summary>通知する相手を読む</summary>
    /// <param name="top">
    /// 先頭に付ける <c>TOP</c> の指定
    /// 無ければ空
    /// 定数だけを渡す (SQL に埋め込むため)
    /// </param>
    /// <param name="userFilter">
    /// 人を絞る条件
    /// 定数だけを渡す (SQL に埋め込むため)
    /// </param>
    /// <param name="addParameters">条件が使うパラメーターを足す処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 通知する相手
    /// 人の番号の順
    /// </returns>
    /// <remarks>人の絞り方と件数の指定だけを変えて、2 つの読み方で共有する。</remarks>
    private Task<IReadOnlyList<NoteRecipient>> ReadRecipientsAsync(string top, string userFilter, Action<SqlCommand> addParameters, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync<IReadOnlyList<NoteRecipient>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT {top}t.UserId, ISNULL(u.DisplayName, N''), c.Id, c.IsDeleted, c.UserId, c.Kind, c.Name, c.Value
                FROM dbo.AppUserSetting AS t
                INNER JOIN dbo.AppUserSetting AS s
                    ON s.UserId = t.UserId AND s.FeatureNo = {NotificationTarget.FeatureNo} AND s.CategoryNo = {NotificationTarget.CategoryNo}
                        AND s.CodeNo = {NotificationTarget.CodeNo} AND s.IsDeleted = 0
                INNER JOIN dbo.NotificationChannel AS c ON c.Id = TRY_CAST(s.Value AS int) AND c.IsDeleted = 0
                LEFT JOIN dbo.AppUser AS u ON u.Id = t.UserId
                WHERE t.FeatureNo = {CodeFeatures.Notes} AND t.CategoryNo = {NotificationFeatureSettings.CategoryNo} AND t.CodeNo = {NotificationFeatureSettings.CodeNo}
                    AND t.Value = N'{NotificationFeatureSettings.On}' AND t.IsDeleted = 0
                    AND {userFilter}
                    AND NOT {FeatureAccessSql.IsDisabled("t.UserId", CodeFeatures.Notes)}
                ORDER BY t.UserId
                """;
            addParameters(command);
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
