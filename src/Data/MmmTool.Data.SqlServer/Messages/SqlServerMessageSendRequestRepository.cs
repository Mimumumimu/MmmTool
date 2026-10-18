using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.Messages;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Messages;

/// <summary>
/// 宛先の読み出しと、メッセージの送信依頼の保存を、SQL Server で行う (<c>dbo.MessageSendRequest</c>)。
/// </summary>
/// <remarks>
/// 読むたびに DB から読む。操作した人 (作成者・更新者・送った人)は <see cref="CurrentUser"/>。特定していないと、依頼を足せない。
/// 宛先は、送る先の値 (<c>NotificationChannel.Value</c>。トピック名・Webhook の URL)を読まない (秘密のため。値は、MmmBatch が送るときに読む)。
/// 依頼の状態を変えるのは MmmBatch で、ここは行を足すだけ。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
public sealed class SqlServerMessageSendRequestRepository(SqlServerDatabase database, CurrentUser currentUser) : IMessageSendRequestRepository
{
    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<IReadOnlyList<MessageRecipient>> GetRecipientsAsync(CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<IReadOnlyList<MessageRecipient>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT c.Id, u.DisplayName, c.Name, c.Kind
                FROM dbo.NotificationChannel AS c
                INNER JOIN dbo.AppUser AS u ON u.Id = c.UserId AND u.IsDeleted = 0
                WHERE c.IsDeleted = 0
                ORDER BY u.Id, c.Id
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var recipients = new List<MessageRecipient>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var kind = (NotificationChannelKind)reader.GetByte(3);
                if (!Enum.IsDefined(kind))
                {
                    throw new InvalidOperationException($"送信先 {reader.GetInt32(0)} の区分が正しくありません ({(int)kind})。");
                }
                recipients.Add(new MessageRecipient(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), kind));
            }
            return recipients;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">送る先の番号が正しくない・タイトルが長すぎる・本文が空か長すぎる。</exception>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    public Task<bool> AddAsync(int channelId, string title, string body, CancellationToken cancellationToken = default)
    {
        MessageSendRequestRow.Validate(channelId, title, body);
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            // 選んだ送信先が、まだ削除されていないときだけ足す (選んだあとに削除された送信先へ、依頼を足さない)
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO dbo.MessageSendRequest (CreatedByUserId, UpdatedByUserId, SenderUserId, ChannelId, Title, Body)
                SELECT @UserId, @UserId, @UserId, @ChannelId, @Title, @Body
                WHERE EXISTS (SELECT 1 FROM dbo.NotificationChannel WHERE Id = @ChannelId AND IsDeleted = 0)
                """;
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@ChannelId", SqlDbType.Int) { Value = channelId });
            command.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, MessageLimits.TitleMaxLength) { Value = title });
            command.Parameters.Add(new SqlParameter("@Body", SqlDbType.NVarChar, MessageLimits.BodyMaxLength) { Value = body });

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }, cancellationToken);
    }
}
