using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Users;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>
/// 自分の送信先に届いたリマインダーを、受け取った通知として、SQL Server の <c>dbo.ReminderSendStatus</c> から読む。
/// </summary>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
/// <remarks>
/// 自分の送信先は <c>NotificationChannel.UserId</c> が自分のもので、共通ヘッダーの列は条件に使わない。
/// 送信済みのものだけを、送った日時の新しい順に読む。
/// 読む量が履歴の長さに比例しないよう、送信先ごとに、索引 <c>IX_ReminderSendStatus_ChannelId_SentAt</c> で新しい順に先頭から件数だけ読み、そのあと、送信先をまたいで並べる。
/// 見出しは「リマインダー」で、本文は、1 行目が件名、備考があれば次の行。形は <see cref="ReminderNotificationText"/> と同じ。リンクはリマインダーのリンク。送る先の値は読まない。
/// 削除したリマインダーの、過去の通知も、一覧に残る。
/// </remarks>
public sealed class SqlServerReceivedReminderSource(SqlServerDatabase database, CurrentUser currentUser) : IReceivedNotificationSource
{
    /// <summary>送信済みを表す状態の値。Pending = 1 / Sent = 2 / Failed = 3 (<c>docs/specs/database.md</c> の <c>ReminderSendStatus</c>)</summary>
    private const byte Sent = 2;

    /// <summary>管理者が、今のユーザーのリマインダーを無効にしているかを調べる条件。無効の人には、何も出さない</summary>
    private static readonly string RemindersDisabled = FeatureAccessSql.IsDisabled("@UserId", CodeFeatures.Reminders);

    /// <summary>新しい順に読む問い合わせ。送信先ごとの先頭を、<c>CROSS APPLY</c> で索引から読む。<see cref="ReadRow"/> の並び</summary>
    private static readonly string LatestSql =
        $"""
        SELECT TOP (@Count) s.Id, r.Title, r.Note, r.Link, s.SentAt
        FROM dbo.NotificationChannel AS c
        CROSS APPLY
        (
            SELECT TOP (@Count) x.Id, x.ReminderId, x.SentAt
            FROM dbo.ReminderSendStatus AS x
            WHERE x.ChannelId = c.Id AND x.Status = @Sent AND x.SentAt < @NotSentAt
            ORDER BY x.SentAt DESC, x.Id DESC
        ) AS s
        INNER JOIN dbo.Reminder AS r ON r.Id = s.ReminderId
        WHERE c.UserId = @UserId AND NOT {RemindersDisabled}
        ORDER BY s.SentAt DESC, s.Id DESC
        """;

    /// <summary>番号を指定して 1 件読む問い合わせ。自分の送信先に届いたものだけ。<see cref="ReadRow"/> の並び</summary>
    private static readonly string OneSql =
        $"""
        SELECT s.Id, r.Title, r.Note, r.Link, s.SentAt
        FROM dbo.ReminderSendStatus AS s
        INNER JOIN dbo.NotificationChannel AS c ON c.Id = s.ChannelId AND c.UserId = @UserId
        INNER JOIN dbo.Reminder AS r ON r.Id = s.ReminderId
        WHERE s.Id = @Id AND s.Status = @Sent AND NOT {RemindersDisabled}
        """;

    /// <inheritdoc />
    public string Key => "reminder";

    /// <inheritdoc />
    public string KindName => "リマインダー";

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public Task<IReadOnlyList<ReceivedNotification>> GetLatestAsync(int count, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync<IReadOnlyList<ReceivedNotification>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = LatestSql;
            command.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = count });
            AddCommonParameters(command, userId);
            command.Parameters.Add(new SqlParameter("@NotSentAt", SqlDbType.DateTimeOffset) { Value = SendTableAccess.NotSentAt });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var items = new List<ReceivedNotification>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(ReadRow(reader));
            }
            return items;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public Task<ReceivedNotification?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync<ReceivedNotification?>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = OneSql;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            AddCommonParameters(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRow(reader) : null;
        }, cancellationToken);
    }

    /// <summary>自分の番号と、送信済みの状態の値を、問い合わせに足す</summary>
    /// <param name="command">問い合わせ</param>
    /// <param name="userId">今のユーザーの番号</param>
    private static void AddCommonParameters(SqlCommand command, int userId)
    {
        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
        command.Parameters.Add(new SqlParameter("@Sent", SqlDbType.TinyInt) { Value = Sent });
    }

    /// <summary>読んでいる行を、受け取った通知にする</summary>
    /// <param name="reader">問い合わせの列を読んでいるリーダー</param>
    /// <returns>受け取った通知</returns>
    private static ReceivedNotification ReadRow(SqlDataReader reader)
    {
        var link = reader.GetString(3).Trim();
        return new ReceivedNotification(
            reader.GetInt32(0),
            ReminderNotificationText.Heading,
            ReminderNotificationText.Body(reader.GetString(1), reader.GetString(2)),
            link.Length == 0 ? null : link,
            reader.GetDateTimeOffset(4));
    }
}
