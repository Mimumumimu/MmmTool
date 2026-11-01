using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MmmBatch.MessageSending.Core;
using MmmBatch.Sending.Core;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>
/// メッセージの通知の状態を、SQL Server の <c>dbo.MessageDelivery</c> から読み、状態を書く。
/// </summary>
/// <remarks>
/// 行を足すのは MmmTool と Web 版。ここは読んで、状態を変えるだけ。読むたびに DB から読む。
/// MmmBatch はユーザーではないので、更新者は 0。更新日時は DB サーバーの時計で書く。複数の MmmBatch の時計のずれを入れないため。
/// 送る先は、通知を受ける人の受け取り先 (<c>dbo.AppUserSetting</c>)から読み、その送信先の値 (<c>dbo.NotificationChannel</c>)を読む。送るため。
/// 受け取り先を指定していない人の分は、送る先が null になる。
/// <c>dbo.Message.UpdatedAt</c> は更新しない。画面に通知の状態を出さないため、端末の差分の取得に、状態の変わりを届ける必要が無い。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerMessageSendStore(SqlServerDatabase database) : IMessageSendStore
{
    /// <summary>読む列 (<see cref="ReadTarget"/> の並び)</summary>
    private static readonly string Select =
        $$"""
        SELECT d.Id, d.MessageId, d.UserId, m.SenderUserId, m.Body, d.Status, d.Attempts, d.CreatedAt, d.UpdatedAt,
               c.Id, c.IsDeleted, c.UserId, c.Kind, c.Name, c.Value, ISNULL(ru.DisplayName, N''), ISNULL(su.DisplayName, N''),
               CASE WHEN cv.Kind = {{(byte)ConversationKind.Group}} THEN cv.Name ELSE N'' END
        FROM dbo.MessageDelivery AS d
        INNER JOIN dbo.Message AS m ON m.Id = d.MessageId
        LEFT JOIN dbo.MessageConversation AS cv ON cv.Id = m.ConversationId
        LEFT JOIN dbo.AppUserSetting AS s ON s.UserId = d.UserId AND s.FeatureNo = {{ReceiveTarget.FeatureNo}} AND s.CategoryNo = {{ReceiveTarget.CategoryNo}}
            AND s.CodeNo = {{ReceiveTarget.CodeNo}} AND s.IsDeleted = 0
        LEFT JOIN dbo.NotificationChannel AS c ON c.Id = TRY_CAST(s.Value AS int)
        LEFT JOIN dbo.AppUser AS ru ON ru.Id = d.UserId
        LEFT JOIN dbo.AppUser AS su ON su.Id = m.SenderUserId
        """;


    /// <inheritdoc />
    /// <remarks>主キー (<c>Id</c>)の降順で先頭から読む。日付・状態では絞らないので、索引は要らない。</remarks>
    public Task<IReadOnlyList<MessageSendTarget>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => ReadAsync(
            "WHERE d.IsDeleted = 0 ORDER BY d.Id DESC OFFSET 0 ROWS FETCH NEXT @Count ROWS ONLY",
            0, count, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 受ける人のメッセージが無効かの判定は、MmmTool と Web 版が宛先を外すときと同じ条件で、<see cref="FeatureAccessSql"/> にある。
    /// </remarks>
    public Task<MessageSendBatch> SkipDisabledAndGetSendableAsync(int maxAttempts, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            // 管理者が無効にした人と、自分で「メッセージの通知を受け取る」をオフにした人 (既定はオンなので、値 0 の行がある人だけ)
            var disabled =
                $"""
                ({FeatureAccessSql.IsDisabled("d.UserId", CodeFeatures.Messages)}
                    OR EXISTS (SELECT 1 FROM dbo.AppUserSetting AS ns
                               WHERE ns.UserId = d.UserId AND ns.FeatureNo = {CodeFeatures.Messages} AND ns.CategoryNo = {NotificationFeatureSettings.CategoryNo}
                                   AND ns.CodeNo = {NotificationFeatureSettings.CodeNo} AND ns.Value = N'{NotificationFeatureSettings.Off}' AND ns.IsDeleted = 0))
                """;
            command.CommandText =
                $"""
                UPDATE d SET Status = @Skipped, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
                FROM dbo.MessageDelivery AS d
                WHERE d.IsDeleted = 0 AND d.Status IN (@Pending, @Failed) AND {disabled};
                SELECT @@ROWCOUNT;
                {Select}
                WHERE d.IsDeleted = 0 AND d.Status IN (@Pending, @Failed) AND d.Attempts < @MaxAttempts
                    AND ((d.Status = @Pending AND (d.Attempts = 0 OR d.UpdatedAt <= DATEADD(second, -@StaleSeconds, SYSDATETIMEOFFSET())))
                        OR (d.Status = @Failed AND d.UpdatedAt <= DATEADD(second, -(SELECT w.Seconds FROM OPENJSON(@FailedWaits) WITH (Attempts int '$.attempts', Seconds int '$.seconds') AS w WHERE w.Attempts = d.Attempts), SYSDATETIMEOFFSET())))
                ORDER BY d.Id;
                """;
            command.Parameters.Add(new SqlParameter("@Pending", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Pending });
            command.Parameters.Add(new SqlParameter("@Failed", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Failed });
            command.Parameters.Add(new SqlParameter("@Skipped", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Skipped });
            command.Parameters.Add(new SqlParameter("@MaxAttempts", SqlDbType.Int) { Value = maxAttempts });

            // 待ち時間は、送り直しの決まり (SendRetryPolicy)から作って渡す。決まりを SQL に写さない
            command.Parameters.Add(new SqlParameter("@StaleSeconds", SqlDbType.Int) { Value = (int)SendRetryPolicy.StalePendingAge.TotalSeconds });
            var failedWaits = JsonSerializer.Serialize(Enumerable.Range(0, maxAttempts).Select(attempts => new { attempts, seconds = (int)SendRetryPolicy.FailedWait(attempts).TotalSeconds }));
            command.Parameters.Add(new SqlParameter("@FailedWaits", SqlDbType.NVarChar, -1) { Value = failedWaits });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            // 1 つ目の結果は止めた件数、2 つ目の結果は送る対象
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var skipped = reader.GetInt32(0);
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
            var targets = new List<MessageSendTarget>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                targets.Add(ReadTarget(reader));
            }
            return new MessageSendBatch(skipped, targets);
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryClaimAsync(MessageDeliveryItem current, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        return (await TryClaimManyAsync([current], cancellationToken).ConfigureAwait(false)).Contains(current.Id);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 読んだときの状態・更新日時と同じ行だけを更新する。別の MmmBatch が先に取っていれば、その行は更新されない。
    /// 行ごとの条件 (番号・状態・更新日時)は、JSON の 1 つのパラメーターで渡す。更新日時はミリ秒までの値で比べる。
    /// </remarks>
    public Task<IReadOnlySet<int>> TryClaimManyAsync(IReadOnlyList<MessageDeliveryItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
        }

        var json = JsonSerializer.Serialize(items.Select(item => new { id = item.Id, status = (byte)item.Status, updatedAt = item.UpdatedAt }));
        return SendTableAccess.RunAsync<IReadOnlySet<int>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE d SET d.Status = @Pending, d.Attempts = d.Attempts + 1, d.UpdatedAt = SYSDATETIMEOFFSET(), d.UpdatedByUserId = 0
                OUTPUT inserted.Id
                FROM dbo.MessageDelivery AS d
                INNER JOIN OPENJSON(@Items) WITH (Id int '$.id', Status tinyint '$.status', UpdatedAt datetimeoffset(3) '$.updatedAt') AS j ON j.Id = d.Id
                WHERE d.Status = j.Status AND d.UpdatedAt = j.UpdatedAt
                """;
            command.Parameters.Add(new SqlParameter("@Pending", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Pending });
            command.Parameters.Add(new SqlParameter("@Items", SqlDbType.NVarChar, -1) { Value = json });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var claimed = new HashSet<int>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                claimed.Add(reader.GetInt32(0));
            }
            return claimed;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task MarkSentAsync(int id, CancellationToken cancellationToken = default)
        => FinishAsync(id, MessageSendStatus.Sent, setSentAt: true, cancellationToken);

    /// <inheritdoc />
    public Task MarkFailedAsync(int id, CancellationToken cancellationToken = default)
        => FinishAsync(id, MessageSendStatus.Failed, setSentAt: false, cancellationToken);

    /// <summary>状態を書く</summary>
    /// <param name="id">通知の状態の番号</param>
    /// <param name="status">書く状態</param>
    /// <param name="setSentAt">送った日時も書くとき true</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新の完了を表すタスク</returns>
    private Task FinishAsync(int id, MessageSendStatus status, bool setSentAt, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                UPDATE dbo.MessageDelivery
                SET Status = @Status{(setSentAt ? ", SentAt = SYSDATETIMEOFFSET()" : "")}, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
                WHERE Id = @Id;
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = (byte)status });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

    /// <summary>条件に合う通知の状態を、送る先と一緒に読む</summary>
    /// <param name="whereAndOrder">WHERE と ORDER BY。パラメーターは <c>@Pending</c>・<c>@Sent</c>・<c>@Failed</c>・<c>@MaxAttempts</c>・<c>@Count</c>。状態で絞るときは、索引 <c>Status</c> で探せるよう、<c>Status</c> の条件から始める</param>
    /// <param name="maxAttempts">送ろうとする回数の上限</param>
    /// <param name="count">読む最大の件数 <c>@Count</c>。使わないときは null</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送る対象</returns>
    private Task<IReadOnlyList<MessageSendTarget>> ReadAsync(string whereAndOrder, int maxAttempts, int? count, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync<IReadOnlyList<MessageSendTarget>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"{Select}\n{whereAndOrder}";
            command.Parameters.Add(new SqlParameter("@Pending", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Pending });
            command.Parameters.Add(new SqlParameter("@Sent", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Sent });
            command.Parameters.Add(new SqlParameter("@Failed", SqlDbType.TinyInt) { Value = (byte)MessageSendStatus.Failed });
            command.Parameters.Add(new SqlParameter("@MaxAttempts", SqlDbType.Int) { Value = maxAttempts });
            if (count is { } limit)
            {
                command.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = limit });
            }
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var targets = new List<MessageSendTarget>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                targets.Add(ReadTarget(reader));
            }
            return targets;
        }, cancellationToken);

    /// <summary>読んでいる行を、<see cref="Select"/> の並びの <see cref="MessageSendTarget"/> にする</summary>
    /// <param name="reader">読んでいる位置のリーダー</param>
    /// <returns>送る対象</returns>
    /// <exception cref="InvalidOperationException">状態・区分が、定義にない値。バグか、手で直した値。</exception>
    private static MessageSendTarget ReadTarget(SqlDataReader reader)
    {
        var status = (MessageSendStatus)reader.GetByte(5);
        if (!Enum.IsDefined(status))
        {
            throw new InvalidOperationException($"メッセージの通知の状態 {reader.GetInt32(0)} の状態が正しくありません: {(int)status}。");
        }

        var request = new MessageDeliveryItem(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetString(4), status, reader.GetByte(6),
            reader.GetDateTimeOffset(7), reader.GetDateTimeOffset(8));

        NotificationChannel? channel = null;
        if (!reader.IsDBNull(9))
        {
            channel = new NotificationChannelRow
            {
                Id = reader.GetInt32(9),
                IsDeleted = reader.GetBoolean(10),
                UserId = reader.GetInt32(11),
                Kind = reader.GetByte(12),
                Name = reader.GetString(13),
                Value = reader.GetString(14),
            }.ToChannel();
        }
        return new MessageSendTarget(request, channel, reader.GetString(15), reader.GetString(16), reader.GetString(17));
    }
}
