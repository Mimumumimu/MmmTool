using System.Data;
using Microsoft.Data.SqlClient;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Reminders;

namespace MmmTool.Data.SqlServer.Messages;

/// <summary>
/// 更新されたメッセージと、その会話に入っている人、更新された参加者の行 (読んだ位置の変わり)を、ユーザーを特定せずに読む。Web 版のサーバーが、会話を開いている端末に、更新を知らせるために使う。
/// </summary>
/// <remarks>
/// 全員分を 1 つの問い合わせにして、索引 <c>Message (UpdatedAt)</c> と <c>MessageConversationMember (UpdatedAt)</c> で、基準より後に更新された行だけを読む。
/// 読むのは番号と日時だけで、本文は読まない。
/// 参加者の行は、削除済みも読む。グループを抜けた人の別の端末に、抜けたことを知らせるため。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerMessageChangeReader(SqlServerDatabase database)
{
    /// <summary>
    /// 基準より後に更新されたメッセージと、その会話の参加者を読む。
    /// </summary>
    /// <param name="since">この日時より後に更新された行を読む</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新されたメッセージの番号と更新日時、参加者の番号の組。次の基準にする DB の今の日時も返す</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。</exception>
    public Task<(IReadOnlyList<MessageChange> Changes, DateTimeOffset Now)> ReadAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
        => SendTableAccess.RunAsync<(IReadOnlyList<MessageChange>, DateTimeOffset)>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT SYSDATETIMEOFFSET();
                SELECT m.Id, m.UpdatedAt, mm.UserId, CAST(0 AS bit)
                FROM dbo.Message AS m
                INNER JOIN dbo.MessageConversationMember AS mm ON mm.ConversationId = m.ConversationId AND mm.IsDeleted = 0
                WHERE m.UpdatedAt > @Since AND m.IsDeleted = 0
                UNION ALL
                SELECT p.Id, p.UpdatedAt, p.UserId, CAST(1 AS bit)
                FROM dbo.MessageConversationMember AS p
                WHERE p.UpdatedAt > @Since
                """;
            command.Parameters.Add(new SqlParameter("@Since", SqlDbType.DateTimeOffset) { Value = since });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var now = reader.GetDateTimeOffset(0);
            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);

            var changes = new List<MessageChange>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                changes.Add(new MessageChange(reader.GetInt32(0), reader.GetDateTimeOffset(1), reader.GetInt32(2), reader.GetBoolean(3)));
            }
            return (changes, now);
        }, cancellationToken);
}

/// <summary>
/// 更新された行 1 件と、知らせる相手 1 人の組。更新された行は、メッセージか、会話の参加者の行のどちらか。参加者の行の更新は、読んだ位置の変わりを表す。
/// </summary>
/// <param name="RowId">更新された行の番号。<paramref name="IsReadPosition"/> が false のときはメッセージの番号、true のときは参加者の行の番号</param>
/// <param name="UpdatedAt">行を最後に更新した日時。DB の時計</param>
/// <param name="UserId">更新を知らせる相手。メッセージのときは会話に入っている人、参加者の行のときはその行の人</param>
/// <param name="IsReadPosition">参加者の行の更新 (読んだ位置の変わり)か。false ならメッセージの更新</param>
public sealed record MessageChange(int RowId, DateTimeOffset UpdatedAt, int UserId, bool IsReadPosition);
