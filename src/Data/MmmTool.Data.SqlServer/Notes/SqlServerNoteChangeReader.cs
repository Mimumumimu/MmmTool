using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Notes.Core;

namespace MmmTool.Data.SqlServer.Notes;

/// <summary>
/// 基準の時刻の後に更新されたノートを、SQL Server の <c>dbo.Note</c> から読む。他の人の作成・更新の通知の元。
/// </summary>
/// <param name="database">SQL Server への入口</param>
/// <remarks>
/// <para>
/// MmmTool (PC の通知ダイアログ)と MmmBatch (外部への通知)が、同じ条件で読むため、ユーザーに依らない読み方を 1 か所に持つ。
/// 読むのは、削除されておらず、公開範囲が「全員」で、終了日が今日以降のノート。「自分だけ」のノートは、書いた人しか読めないので、他の人への通知にならない。
/// 更新日時 <c>UpdatedAt</c> の索引 <c>IX_Note_UpdatedAt</c> で、基準の後を探す。最後に更新した人 (<c>UpdatedByUserId</c>)は、条件に使わず、読んだ値として返す。
/// 更新した人の名前は、表示のためだけに、更新した人の表 <c>dbo.AppUser</c> を主キーで結合して、ノートと一緒に 1 回で読む。読む行は、周期の間に更新されたノートだけで少ない。
/// 問い合わせを 2 回に分けると、1 回目のあとで通信が切れたときに、その更新の通知を失うため。
/// </para>
/// <para>
/// 基準にする時刻は、DB サーバーの時計で返す。更新日時も DB サーバーの時計で書かれるので、読む側の PC の時計のずれを入れないため。
/// </para>
/// </remarks>
public sealed class SqlServerNoteChangeReader(SqlServerDatabase database)
{
    /// <summary>オブジェクト名が正しくない (表が無い)ときの SQL Server のエラー番号</summary>
    private const int InvalidObjectName = 208;

    /// <summary>基準の時刻の後に更新されたノートを読む</summary>
    /// <param name="since">この時刻より後に更新されたものを読む。null のときは、ノートを読まず、データベースサーバーの今の時刻だけを返す</param>
    /// <param name="today">今日。終了日がこの日以降のノートだけを読む</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新されたノート (更新日時の古い順)と、読んだ時点のデータベースサーバーの時刻</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public Task<NoteChanges> ReadAsync(DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken = default)
        => database.RunAsync(async connection =>
        {
            try
            {
                return await ReadCoreAsync(connection, since, today, cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                throw new DataFileException("ノートの表がデータベースにありません。管理者に、003_AddNote.sql を実行してもらってください。", ex);
            }
        }, cancellationToken);

    /// <summary>ノートが、いまも他の人への通知の対象か確かめる</summary>
    /// <param name="noteId">ノートの番号</param>
    /// <param name="today">今日。終了日がこの日以降かを見る</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除されておらず、公開範囲が「全員」で、終了日が今日以降なら true。送る予定に入れたあとで、非公開にされた・削除された・期間が過去になったノートを、送る直前に外すために使う</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public Task<bool> IsNoticeTargetAsync(int noteId, DateOnly today, CancellationToken cancellationToken = default)
        => database.RunAsync(async connection =>
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM dbo.Note WHERE Id = @Id AND IsDeleted = 0 AND IsPrivate = 0 AND EndDate >= @Today";
                command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = noteId });
                command.Parameters.Add(new SqlParameter("@Today", SqlDbType.Date) { Value = today.ToDateTime(TimeOnly.MinValue) });
                return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                throw new DataFileException("ノートの表がデータベースにありません。管理者に、003_AddNote.sql を実行してもらってください。", ex);
            }
        }, cancellationToken);

    /// <summary>接続を開いたあとの、実際の読み込み</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="since">基準の時刻。null なら時刻だけ</param>
    /// <param name="today">今日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読んだ結果</returns>
    private static async Task<NoteChanges> ReadCoreAsync(SqlConnection connection, DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        // 時刻を先に読む。読む間に更新された分は、次の周期で、戻して読むときに拾う
        command.CommandText = since is null
            ? "SELECT SYSDATETIMEOFFSET();"
            : """
              SELECT SYSDATETIMEOFFSET();
              SELECT n.Id, n.Title, n.UpdatedByUserId, n.CreatedAt, n.UpdatedAt, n.StartDate, n.EndDate, ISNULL(u.DisplayName, N'')
              FROM dbo.Note AS n
              LEFT JOIN dbo.AppUser AS u ON u.Id = n.UpdatedByUserId
              WHERE n.UpdatedAt > @Since AND n.IsDeleted = 0 AND n.IsPrivate = 0 AND n.EndDate >= @Today
              ORDER BY n.UpdatedAt, n.Id;
              """;
        if (since is { } from)
        {
            command.Parameters.Add(new SqlParameter("@Since", SqlDbType.DateTimeOffset, 0) { Value = from, Scale = 3 });
            command.Parameters.Add(new SqlParameter("@Today", SqlDbType.Date) { Value = today.ToDateTime(TimeOnly.MinValue) });
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var serverNow = reader.GetDateTimeOffset(0);
        var changes = new List<NoteChange>();
        if (since is not null && await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                changes.Add(new NoteChange(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetDateTimeOffset(3),
                    reader.GetDateTimeOffset(4),
                    DateOnly.FromDateTime(reader.GetDateTime(5)),
                    DateOnly.FromDateTime(reader.GetDateTime(6)),
                    reader.GetString(7)));
            }
        }

        return new NoteChanges(serverNow, changes);
    }
}
