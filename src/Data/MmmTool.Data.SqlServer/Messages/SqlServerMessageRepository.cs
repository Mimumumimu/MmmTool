using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Messages;

/// <summary>
/// 会話とメッセージの読み出しと書き込みを、SQL Server で行う。
/// 表は、<c>dbo.MessageConversation</c>・<c>dbo.MessageConversationMember</c>・<c>dbo.Message</c>・<c>dbo.MessageDelivery</c>。
/// </summary>
/// <remarks>
/// 読むたびに DB から読む。操作した人は <see cref="CurrentUser"/>。特定していないと、読み書きできない。
/// 通知の状態を変えるのは MmmBatch で、ここは行を足すだけ。受け取り先の送信先の値は、読まない。
/// 差分の取得は、会話の数に関係なく、<c>MessageDelivery (UserId, UpdatedAt)</c>・<c>Message (SenderUserId, UpdatedAt)</c>・
/// <c>MessageConversationMember (UserId, UpdatedAt)</c> の索引 3 回で読む。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
public sealed class SqlServerMessageRepository(SqlServerDatabase database, CurrentUser currentUser) : IMessageRepository
{
    /// <summary>1 対 1 の会話の種類の値 <c>MessageConversation.Kind</c></summary>
    private const byte DirectKind = (byte)ConversationKind.Direct;

    /// <summary>メッセージを、返信元と通知の状態つきで読む問い合わせの、選ぶ列と結合</summary>
    /// <remarks>
    /// 通知の状態は、自分が送ったメッセージにだけ付ける。失敗が 1 つでもあれば失敗、なければ送信待ちが 1 つでもあれば送信待ち、それ以外は送信済み。
    /// </remarks>
    private const string MessageSelect =
        """
        SELECT m.Id, m.ConversationId, m.SenderUserId, ISNULL(su.DisplayName, N''), m.ReplyToMessageId, rsu.DisplayName, r.Body, m.Body, m.CreatedAt, m.UpdatedAt,
            CASE WHEN m.SenderUserId = @Me THEN
                (SELECT CASE WHEN SUM(CASE WHEN d.Status = 3 THEN 1 ELSE 0 END) > 0 THEN 3
                             WHEN SUM(CASE WHEN d.Status = 1 THEN 1 ELSE 0 END) > 0 THEN 1
                             ELSE 2 END
                 FROM dbo.MessageDelivery AS d WHERE d.MessageId = m.Id AND d.IsDeleted = 0)
            END
        FROM dbo.Message AS m
        LEFT JOIN dbo.AppUser AS su ON su.Id = m.SenderUserId
        LEFT JOIN dbo.Message AS r ON r.Id = m.ReplyToMessageId AND m.ReplyToMessageId > 0
        LEFT JOIN dbo.AppUser AS rsu ON rsu.Id = r.SenderUserId
        """;

    /// <summary>管理者がメッセージを無効にした人かを調べる条件。人の番号を表す式は、<c>{0}</c> に入れる</summary>
    private static readonly string MessagesDisabled = FeatureAccessSql.IsDisabled("{0}", CodeFeatures.Messages);

    /// <summary>その人が、メッセージを受け取れるかを調べる条件。その人が、メッセージの受け取り先を指定し、その送信先が削除されておらず、管理者がメッセージを無効にしていない</summary>
    /// <remarks>人の番号を表す式は、<c>{0}</c> に入れる。</remarks>
    private static readonly string ReceiveTargetExists =
        $$"""
        (EXISTS (SELECT 1 FROM dbo.AppUserSetting AS s
                INNER JOIN dbo.NotificationChannel AS nc ON nc.Id = TRY_CAST(s.Value AS int) AND nc.IsDeleted = 0
                WHERE s.UserId = {0} AND s.FeatureNo = {{ReceiveTarget.FeatureNo}} AND s.CategoryNo = {{ReceiveTarget.CategoryNo}} AND s.CodeNo = {{ReceiveTarget.CodeNo}} AND s.IsDeleted = 0)
            AND NOT {{MessagesDisabled}})
        """;

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public Task<MessageSyncResult> GetStartAsync(CancellationToken cancellationToken = default)
    {
        var me = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            var baseTime = await ReadNowAsync(connection, cancellationToken).ConfigureAwait(false);
            var conversations = await ReadConversationsAsync(connection, me, null, cancellationToken).ConfigureAwait(false);
            return new MessageSyncResult([], conversations, baseTime);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(int conversationId, int beforeMessageId, int count, CancellationToken cancellationToken = default)
    {
        var me = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync<IReadOnlyList<ChatMessage>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                {MessageSelect.Replace("SELECT m.Id", "SELECT TOP (@Count) m.Id", StringComparison.Ordinal)}
                WHERE m.ConversationId = @ConversationId AND m.IsDeleted = 0 AND (@Before = 0 OR m.Id < @Before)
                    AND EXISTS (SELECT 1 FROM dbo.MessageConversationMember AS mm WHERE mm.ConversationId = m.ConversationId AND mm.UserId = @Me AND mm.IsDeleted = 0)
                ORDER BY m.Id DESC
                """;
            command.Parameters.Add(Int("@Me", me));
            command.Parameters.Add(Int("@ConversationId", conversationId));
            command.Parameters.Add(Int("@Before", beforeMessageId));
            command.Parameters.Add(Int("@Count", count));
            return await ReadMessagesAsync(command, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<MessageSyncResult> GetChangesAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var me = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            var baseTime = await ReadNowAsync(connection, cancellationToken).ConfigureAwait(false);

            // 自分宛の新着と通知の状態の変わり、自分が送ったメッセージの変わりを、それぞれ索引 1 回で読む
            await using var messageCommand = connection.CreateCommand();
            messageCommand.CommandText =
                $"""
                WITH Changed AS (
                    SELECT d.MessageId AS Id FROM dbo.MessageDelivery AS d WHERE d.UserId = @Me AND d.UpdatedAt > @Since
                    UNION
                    SELECT m2.Id FROM dbo.Message AS m2 WHERE m2.SenderUserId = @Me AND m2.UpdatedAt > @Since)
                {MessageSelect}
                WHERE m.IsDeleted = 0 AND m.Id IN (SELECT Id FROM Changed)
                ORDER BY m.Id
                """;
            messageCommand.Parameters.Add(Int("@Me", me));
            messageCommand.Parameters.Add(new SqlParameter("@Since", SqlDbType.DateTimeOffset) { Value = since });
            var messages = await ReadMessagesAsync(messageCommand, cancellationToken).ConfigureAwait(false);

            // 自分が新しく入った会話
            var conversationIds = new HashSet<int>(messages.Select(message => message.ConversationId));
            await using var memberCommand = connection.CreateCommand();
            memberCommand.CommandText =
                "SELECT ConversationId FROM dbo.MessageConversationMember WHERE UserId = @Me AND UpdatedAt > @Since AND IsDeleted = 0";
            memberCommand.Parameters.Add(Int("@Me", me));
            memberCommand.Parameters.Add(new SqlParameter("@Since", SqlDbType.DateTimeOffset) { Value = since });
            await using (var reader = await memberCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    conversationIds.Add(reader.GetInt32(0));
                }
            }

            var conversations = conversationIds.Count == 0
                ? []
                : await ReadConversationsAsync(connection, me, conversationIds, cancellationToken).ConfigureAwait(false);
            return new MessageSyncResult(messages, conversations, baseTime);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> FindDirectConversationAsync(int peerUserId, CancellationToken cancellationToken = default)
    {
        var me = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, connection => FindDirectAsync(connection, null, me, peerUserId, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> FindConversationOfMessageAsync(int messageId, CancellationToken cancellationToken = default)
    {
        var me = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT m.ConversationId FROM dbo.Message AS m
                WHERE m.Id = @MessageId AND m.IsDeleted = 0
                    AND EXISTS (SELECT 1 FROM dbo.MessageConversationMember AS mm WHERE mm.ConversationId = m.ConversationId AND mm.UserId = @Me AND mm.IsDeleted = 0)
                """;
            command.Parameters.Add(Int("@Me", me));
            command.Parameters.Add(Int("@MessageId", messageId));
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is int conversationId ? conversationId : 0;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MessagePerson>> SearchPeopleAsync(string prefix, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        _ = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync<IReadOnlyList<MessagePerson>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT TOP (@Count) u.Id, u.DisplayName
                FROM dbo.AppUser AS u
                WHERE u.IsDeleted = 0 AND u.ValidFrom <= CAST(GETDATE() AS date) AND u.ValidTo >= CAST(GETDATE() AS date)
                    AND u.DisplayName LIKE @Pattern ESCAPE N'\'
                    AND {string.Format(CultureInfo.InvariantCulture, ReceiveTargetExists, "u.Id")}
                ORDER BY u.DisplayName, u.Id
                """;
            command.Parameters.Add(Int("@Count", count));
            command.Parameters.Add(new SqlParameter("@Pattern", SqlDbType.NVarChar, 120) { Value = EscapeLike(prefix.Trim()) + "%" });

            var people = new List<MessagePerson>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                people.Add(new MessagePerson(reader.GetInt32(0), reader.GetString(1)));
            }
            return people;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> CanReceiveAsync(int userId, CancellationToken cancellationToken = default)
    {
        _ = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, connection => CanReceiveAsync(connection, null, userId, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool?> CanPeerReceiveAsync(int conversationId, CancellationToken cancellationToken = default)
    {
        var me = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync<bool?>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT c.Kind, CASE WHEN {string.Format(CultureInfo.InvariantCulture, ReceiveTargetExists, "ISNULL(peer.UserId, @Me)")} THEN 1 ELSE 0 END
                FROM dbo.MessageConversation AS c
                INNER JOIN dbo.MessageConversationMember AS mine ON mine.ConversationId = c.Id AND mine.UserId = @Me AND mine.IsDeleted = 0
                OUTER APPLY (SELECT TOP 1 mm.UserId FROM dbo.MessageConversationMember AS mm
                             WHERE mm.ConversationId = c.Id AND mm.UserId <> @Me AND mm.IsDeleted = 0 ORDER BY mm.Id) AS peer
                WHERE c.Id = @ConversationId AND c.IsDeleted = 0
                """;
            command.Parameters.Add(Int("@Me", me));
            command.Parameters.Add(Int("@ConversationId", conversationId));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
            return reader.GetByte(0) != DirectKind || reader.GetInt32(1) == 1;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">本文が空か長すぎる・会話の番号と相手の番号の指定が正しくない。</exception>
    public Task<SendMessageResult> SendAsync(int conversationId, int peerUserId, string body, int replyToMessageId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!MessageDraft.IsValidBody(body))
        {
            throw new ArgumentException($"本文は、空でない {MessageLimits.BodyMaxLength} 文字までにしてください。", nameof(body));
        }
        if ((conversationId > 0) == (peerUserId > 0) || replyToMessageId < 0)
        {
            throw new ArgumentException("会話の番号か、相手の番号の、どちらか 1 つだけを指定してください。");
        }

        var me = SendTableAccess.RequireUserId(currentUser);
        var trimmed = body.Trim();
        return SendTableAccess.RunAsync(database, async connection =>
        {
            // 管理者がメッセージを無効にした人は、送れない。画面に出ない機能なので、会話が無いものとして扱う
            if (await IsMessagesDisabledAsync(connection, null, me, cancellationToken).ConfigureAwait(false))
            {
                return SendMessageResult.Failed(SendMessageOutcome.NotMember);
            }

            // 自分の受け取り先を指定していない人は、送れない。返事を受け取れない人が、一方的に送る形にしないため
            if (!await CanReceiveAsync(connection, null, me, cancellationToken).ConfigureAwait(false))
            {
                return SendMessageResult.Failed(SendMessageOutcome.SelfNoReceiveTarget);
            }

            // 新しい 1 対 1 は、相手が受け取り先を指定しているときだけ作る (指定していないとき、空の会話を残さない)
            if (conversationId == 0 && !await CanReceiveAsync(connection, null, peerUserId, cancellationToken).ConfigureAwait(false))
            {
                return SendMessageResult.Failed(SendMessageOutcome.NoReceiveTarget);
            }

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var result = await SendInTransactionAsync(connection, transaction, me, conversationId, peerUserId, trimmed, replyToMessageId, cancellationToken).ConfigureAwait(false);
            if (result.Outcome == SendMessageOutcome.Sent)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            return result;
        }, cancellationToken);
    }

    /// <summary>トランザクションの中で、会話を決めて、メッセージと通知の状態を書く</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">開始済みのトランザクション</param>
    /// <param name="me">送る人の番号</param>
    /// <param name="conversationId">会話の番号。無いときは 0</param>
    /// <param name="peerUserId">会話の番号が 0 のとき、1 対 1 の相手の番号</param>
    /// <param name="body">前後の空白を取った本文</param>
    /// <param name="replyToMessageId">返信元の番号。返信でなければ 0</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>結果。送れたときだけ、呼ぶ側がコミットする</returns>
    private static async Task<SendMessageResult> SendInTransactionAsync(
        SqlConnection connection, SqlTransaction transaction, int me, int conversationId, int peerUserId, string body, int replyToMessageId, CancellationToken cancellationToken)
    {
        var kind = DirectKind;
        if (conversationId == 0)
        {
            if (!await UserExistsAsync(connection, transaction, peerUserId, cancellationToken).ConfigureAwait(false))
            {
                return SendMessageResult.Failed(SendMessageOutcome.NotMember);
            }

            // 同じ 2 人の組を、同時に作らないよう、組ごとの鍵で順番に処理する
            await GetLockAsync(connection, transaction, $"MessageDirect:{Math.Min(me, peerUserId)}-{Math.Max(me, peerUserId)}", cancellationToken).ConfigureAwait(false);
            conversationId = await FindDirectAsync(connection, transaction, me, peerUserId, cancellationToken).ConfigureAwait(false);
            if (conversationId == 0)
            {
                conversationId = await CreateDirectAsync(connection, transaction, me, peerUserId, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            var found = await ReadKindIfMemberAsync(connection, transaction, me, conversationId, cancellationToken).ConfigureAwait(false);
            if (found is null)
            {
                return SendMessageResult.Failed(SendMessageOutcome.NotMember);
            }
            kind = found.Value;
        }

        var recipients = await ReadRecipientsAsync(connection, transaction, me, conversationId, cancellationToken).ConfigureAwait(false);
        if (kind == DirectKind)
        {
            foreach (var recipient in recipients)
            {
                if (!await CanReceiveAsync(connection, transaction, recipient, cancellationToken).ConfigureAwait(false))
                {
                    return SendMessageResult.Failed(SendMessageOutcome.NoReceiveTarget);
                }
            }
        }
        else
        {
            // グループは、メッセージを無効にした人には、通知の状態を書かない。ほかの参加者には送る
            var allowed = new List<int>();
            foreach (var recipient in recipients)
            {
                if (!await IsMessagesDisabledAsync(connection, transaction, recipient, cancellationToken).ConfigureAwait(false))
                {
                    allowed.Add(recipient);
                }
            }
            recipients = allowed;
        }

        if (replyToMessageId > 0 && !await ReplyExistsAsync(connection, transaction, conversationId, replyToMessageId, cancellationToken).ConfigureAwait(false))
        {
            return SendMessageResult.Failed(SendMessageOutcome.ReplyInvalid);
        }

        int messageId;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO dbo.Message (CreatedByUserId, UpdatedByUserId, ConversationId, SenderUserId, ReplyToMessageId, Body)
                OUTPUT inserted.Id
                VALUES (@Me, @Me, @ConversationId, @Me, @ReplyTo, @Body)
                """;
            insert.Parameters.Add(Int("@Me", me));
            insert.Parameters.Add(Int("@ConversationId", conversationId));
            insert.Parameters.Add(Int("@ReplyTo", replyToMessageId));
            insert.Parameters.Add(new SqlParameter("@Body", SqlDbType.NVarChar, MessageLimits.BodyMaxLength) { Value = body });
            messageId = (int)(await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }

        foreach (var recipient in recipients)
        {
            await using var delivery = connection.CreateCommand();
            delivery.Transaction = transaction;
            delivery.CommandText = "INSERT INTO dbo.MessageDelivery (CreatedByUserId, UpdatedByUserId, MessageId, UserId) VALUES (@Me, @Me, @MessageId, @UserId)";
            delivery.Parameters.Add(Int("@Me", me));
            delivery.Parameters.Add(Int("@MessageId", messageId));
            delivery.Parameters.Add(Int("@UserId", recipient));
            await delivery.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        return new SendMessageResult(SendMessageOutcome.Sent, conversationId, messageId);
    }

    /// <summary>DB の今の日時を読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>DB の時計の今の日時。次の差分の基準になる</returns>
    private static async Task<DateTimeOffset> ReadNowAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SYSDATETIMEOFFSET()";
        return (DateTimeOffset)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>自分の会話の要約を読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="me">自分の番号</param>
    /// <param name="conversationIds">読む会話の番号。null なら、自分の会話すべて</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>メッセージのある会話の要約。最後のメッセージが新しい順</returns>
    private static async Task<IReadOnlyList<ConversationSummary>> ReadConversationsAsync(
        SqlConnection connection, int me, IReadOnlyCollection<int>? conversationIds, CancellationToken cancellationToken)
    {
        // 番号は int だけなので、そのまま文に入れても安全
        var filter = conversationIds is null
            ? ""
            : "AND c.Id IN (" + string.Join(',', conversationIds.Select(id => id.ToString(CultureInfo.InvariantCulture))) + ")";
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT c.Id, c.Kind, c.Name, ISNULL(peer.UserId, @Me), ISNULL(pu.DisplayName, N''),
                CASE WHEN {string.Format(CultureInfo.InvariantCulture, ReceiveTargetExists, "ISNULL(peer.UserId, @Me)")} THEN 1 ELSE 0 END,
                lastMessage.Id, lastMessage.Body, lastMessage.CreatedAt
            FROM dbo.MessageConversation AS c
            INNER JOIN dbo.MessageConversationMember AS mine ON mine.ConversationId = c.Id AND mine.UserId = @Me AND mine.IsDeleted = 0
            OUTER APPLY (SELECT TOP 1 mm.UserId FROM dbo.MessageConversationMember AS mm
                         WHERE mm.ConversationId = c.Id AND mm.UserId <> @Me AND mm.IsDeleted = 0 ORDER BY mm.Id) AS peer
            LEFT JOIN dbo.AppUser AS pu ON pu.Id = ISNULL(peer.UserId, @Me)
            CROSS APPLY (SELECT TOP 1 m.Id, m.Body, m.CreatedAt FROM dbo.Message AS m
                         WHERE m.ConversationId = c.Id AND m.IsDeleted = 0 ORDER BY m.Id DESC) AS lastMessage
            WHERE c.IsDeleted = 0 {filter}
            ORDER BY lastMessage.Id DESC
            """;
        command.Parameters.Add(Int("@Me", me));

        var summaries = new List<ConversationSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var kind = (ConversationKind)reader.GetByte(1);
            if (!Enum.IsDefined(kind))
            {
                throw new InvalidOperationException($"会話 {reader.GetInt32(0)} の種類が正しくありません: {(int)kind}。");
            }
            var isDirect = kind == ConversationKind.Direct;
            summaries.Add(new ConversationSummary(
                reader.GetInt32(0), kind, isDirect ? reader.GetString(4) : reader.GetString(2), isDirect ? reader.GetInt32(3) : 0,
                !isDirect || reader.GetInt32(5) == 1, reader.GetInt32(6), FirstLine(reader.GetString(7)), reader.GetDateTimeOffset(8)));
        }
        return summaries;
    }

    /// <summary>問い合わせを実行して、メッセージを読む</summary>
    /// <param name="command">列の並びが <see cref="MessageSelect"/> と同じ問い合わせ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読んだメッセージ。問い合わせの並びのまま</returns>
    private static async Task<IReadOnlyList<ChatMessage>> ReadMessagesAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            MessageSendStatus? status = reader.IsDBNull(10) ? null : (MessageSendStatus)reader.GetInt32(10);
            messages.Add(new ChatMessage(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : FirstLine(reader.GetString(6)),
                reader.GetString(7), reader.GetDateTimeOffset(8), reader.GetDateTimeOffset(9), status));
        }
        return messages;
    }

    /// <summary>相手との 1 対 1 の会話を探す</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション。無ければ null</param>
    /// <param name="me">自分の番号</param>
    /// <param name="peerUserId">相手の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>会話の番号。無ければ 0</returns>
    private static async Task<int> FindDirectAsync(SqlConnection connection, SqlTransaction? transaction, int me, int peerUserId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT TOP 1 c.Id
            FROM dbo.MessageConversation AS c
            WHERE c.Kind = 1 AND c.IsDeleted = 0
                AND EXISTS (SELECT 1 FROM dbo.MessageConversationMember AS a WHERE a.ConversationId = c.Id AND a.UserId = @Me AND a.IsDeleted = 0)
                AND EXISTS (SELECT 1 FROM dbo.MessageConversationMember AS b WHERE b.ConversationId = c.Id AND b.UserId = @Peer AND b.IsDeleted = 0)
                AND (SELECT COUNT(*) FROM dbo.MessageConversationMember AS x WHERE x.ConversationId = c.Id AND x.IsDeleted = 0) = CASE WHEN @Me = @Peer THEN 1 ELSE 2 END
            ORDER BY c.Id
            """;
        command.Parameters.Add(Int("@Me", me));
        command.Parameters.Add(Int("@Peer", peerUserId));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is int id ? id : 0;
    }

    /// <summary>1 対 1 の会話と、その参加者を作る</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="me">自分の番号</param>
    /// <param name="peerUserId">相手の番号。自分の番号なら、参加者は自分 1 人</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>作った会話の番号</returns>
    private static async Task<int> CreateDirectAsync(SqlConnection connection, SqlTransaction transaction, int me, int peerUserId, CancellationToken cancellationToken)
    {
        int conversationId;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO dbo.MessageConversation (CreatedByUserId, UpdatedByUserId, Kind) OUTPUT inserted.Id VALUES (@Me, @Me, 1)";
            insert.Parameters.Add(Int("@Me", me));
            conversationId = (int)(await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }

        foreach (var userId in me == peerUserId ? [me] : new[] { me, peerUserId })
        {
            await using var member = connection.CreateCommand();
            member.Transaction = transaction;
            member.CommandText = "INSERT INTO dbo.MessageConversationMember (CreatedByUserId, UpdatedByUserId, ConversationId, UserId) VALUES (@Me, @Me, @ConversationId, @UserId)";
            member.Parameters.Add(Int("@Me", me));
            member.Parameters.Add(Int("@ConversationId", conversationId));
            member.Parameters.Add(Int("@UserId", userId));
            await member.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        return conversationId;
    }

    /// <summary>トランザクションの終わりまで続く、名前つきの鍵を取る</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="resource">鍵の名前</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>鍵を取れたことを表すタスク</returns>
    /// <exception cref="DataFileException">待ちきれず、鍵を取れなかった。</exception>
    private static async Task GetLockAsync(SqlConnection connection, SqlTransaction transaction, string resource, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DECLARE @Result int; EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000; SELECT @Result;";
        command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255) { Value = resource });
        var result = (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        if (result < 0)
        {
            const string Message = "会話を作る順番を待ちきれませんでした。もう一度送ってください。";
            throw new DataFileException(Message, new InvalidOperationException(Message));
        }
    }

    /// <summary>自分が参加している会話の種類を読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="me">自分の番号</param>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>会話の種類の値。自分が参加していない・会話が無いときは null</returns>
    private static async Task<byte?> ReadKindIfMemberAsync(SqlConnection connection, SqlTransaction transaction, int me, int conversationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT c.Kind FROM dbo.MessageConversation AS c
            WHERE c.Id = @ConversationId AND c.IsDeleted = 0
                AND EXISTS (SELECT 1 FROM dbo.MessageConversationMember AS mm WHERE mm.ConversationId = c.Id AND mm.UserId = @Me AND mm.IsDeleted = 0)
            """;
        command.Parameters.Add(Int("@Me", me));
        command.Parameters.Add(Int("@ConversationId", conversationId));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte kind ? kind : null;
    }

    /// <summary>通知を受ける人を読む。送った人以外の参加者で、自分だけの会話は自分</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="me">自分の番号</param>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>通知を受ける人の番号</returns>
    private static async Task<IReadOnlyList<int>> ReadRecipientsAsync(SqlConnection connection, SqlTransaction transaction, int me, int conversationId, CancellationToken cancellationToken)
    {
        var members = new List<int>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT UserId FROM dbo.MessageConversationMember WHERE ConversationId = @ConversationId AND IsDeleted = 0 ORDER BY Id";
            command.Parameters.Add(Int("@ConversationId", conversationId));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                members.Add(reader.GetInt32(0));
            }
        }

        var others = members.Where(userId => userId != me).ToList();
        return others.Count == 0 ? [me] : others;
    }

    /// <summary>人が使える状態で存在するか調べる</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="userId">人の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除されておらず、今日使える期間にいるとき true</returns>
    private static async Task<bool> UserExistsAsync(SqlConnection connection, SqlTransaction transaction, int userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM dbo.AppUser WHERE Id = @UserId AND IsDeleted = 0 AND ValidFrom <= CAST(GETDATE() AS date) AND ValidTo >= CAST(GETDATE() AS date)";
        command.Parameters.Add(Int("@UserId", userId));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <summary>返信元が、同じ会話のメッセージか調べる</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="messageId">返信元の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>同じ会話のメッセージのとき true</returns>
    private static async Task<bool> ReplyExistsAsync(SqlConnection connection, SqlTransaction transaction, int conversationId, int messageId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM dbo.Message WHERE Id = @MessageId AND ConversationId = @ConversationId AND IsDeleted = 0";
        command.Parameters.Add(Int("@MessageId", messageId));
        command.Parameters.Add(Int("@ConversationId", conversationId));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <summary>人が、受け取り先を指定していて、その送信先が削除されていないか調べる</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション。無ければ null</param>
    /// <param name="userId">人の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>受け取れるとき true</returns>
    private static async Task<bool> CanReceiveAsync(SqlConnection connection, SqlTransaction? transaction, int userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT CASE WHEN {string.Format(CultureInfo.InvariantCulture, ReceiveTargetExists, "@UserId")} THEN 1 ELSE 0 END";
        command.Parameters.Add(Int("@UserId", userId));
        return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! == 1;
    }

    /// <summary>管理者が、その人のメッセージを無効にしているか調べる</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション。無ければ null</param>
    /// <param name="userId">人の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>無効のとき true。設定が無い人は有効なので false</returns>
    private static async Task<bool> IsMessagesDisabledAsync(SqlConnection connection, SqlTransaction? transaction, int userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT CASE WHEN {string.Format(CultureInfo.InvariantCulture, MessagesDisabled, "@UserId")} THEN 1 ELSE 0 END";
        command.Parameters.Add(Int("@UserId", userId));
        return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! == 1;
    }

    /// <summary>整数のパラメーターを作る</summary>
    /// <param name="name">パラメーターの名前</param>
    /// <param name="value">値</param>
    /// <returns>整数のパラメーター</returns>
    private static SqlParameter Int(string name, int value) => new(name, SqlDbType.Int) { Value = value };

    /// <summary>LIKE の特別な文字を、そのままの文字として扱えるよう、エスケープする</summary>
    /// <param name="text">探す文字</param>
    /// <returns>エスケープした文字。エスケープの文字は、バックスラッシュ</returns>
    private static string EscapeLike(string text)
        => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal);

    /// <summary>本文の 1 行目を取る</summary>
    /// <param name="text">本文</param>
    /// <returns>最初の改行より前の文字</returns>
    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? text : text[..end];
    }
}
