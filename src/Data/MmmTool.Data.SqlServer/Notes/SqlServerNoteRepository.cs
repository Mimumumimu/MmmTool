using MmmTool.Data.Notes;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Notes.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Notes;

/// <summary>ノートを SQL Server の dbo.Note に保存する</summary>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
/// <remarks>
/// 読み書きの流れは <see cref="NoteRepository"/> にあり、ローカルの SQLite の保存先と共通。
/// ここに残すのは、SQL Server だけの SQL と名前の引き方と他の人の更新の読み方。
/// 読むたびに DB から読む。
/// 読むのは表示している日の分だけ。
/// 操作した人 (作成者・更新者・書いた人)は <see cref="CurrentUser"/>。
/// 特定していないと読み書きできない。
/// 更新日時は更新の SQL の中で DB サーバーの時計を使って書く。
/// </remarks>
public sealed class SqlServerNoteRepository(SqlServerDatabase database, CurrentUser currentUser)
    : NoteRepository(new SqlServerGateway(database, currentUser, "ノート"))
{
    /// <inheritdoc />
    public override bool IsShared => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// まだ特定していない
    /// 呼ぶ側の順序のバグ
    /// </exception>
    public override int CurrentUserId => currentUser.Id;

    /// <inheritdoc />
    /// <remarks>
    /// 書いた人と更新した人の名前はノートと一緒に <c>dbo.AppUser</c> を外部結合して読む。
    /// 表示のための名前で、検索や索引には使わない。
    /// </remarks>
    protected override string NameColumns => ", ISNULL(a.DisplayName, N''), ISNULL(u.DisplayName, N'')";

    /// <inheritdoc />
    protected override string NameJoins =>
        """

        LEFT JOIN dbo.AppUser AS a ON a.Id = n.AuthorUserId
        LEFT JOIN dbo.AppUser AS u ON u.Id = n.UpdatedByUserId
        """;

    /// <inheritdoc />
    protected override string WriteOtherEventDatesSql =>
        """
        UPDATE a
        SET a.Value = j.Value, a.IsDeleted = 0, a.UpdatedAt = SYSDATETIMEOFFSET(), a.UpdatedByUserId = @user
        FROM dbo.NoteAttribute AS a
        INNER JOIN OPENJSON(@update) WITH (CodeNo int '$.codeNo', Value nvarchar(200) '$.value') AS j ON j.CodeNo = a.CodeNo
        WHERE a.NoteId = @noteId AND a.CategoryNo = @category;

        UPDATE dbo.NoteAttribute
        SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user
        WHERE NoteId = @noteId AND CategoryNo = @category
          AND CodeNo IN (SELECT CAST([value] AS int) FROM OPENJSON(@delete));

        INSERT INTO dbo.NoteAttribute (CreatedByUserId, UpdatedByUserId, NoteId, CategoryNo, CodeNo, Value)
        SELECT @user, @user, @noteId, @category, j.CodeNo, j.Value
        FROM OPENJSON(@insert) WITH (CodeNo int '$.codeNo', Value nvarchar(200) '$.value') AS j;
        """;

    /// <inheritdoc />
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public override Task<NoteChange?> FindAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = Gateway.RequireUserId();
        return Gateway.ReadAsync<NoteChange?>(
            async session =>
            {
                var rows = await session.QueryAsync(
                    """
                    SELECT Id, Title, UpdatedByUserId, CreatedAt, UpdatedAt, StartDate, EndDate
                    FROM dbo.Note
                    WHERE Id = @id AND IsDeleted = 0 AND (IsPrivate = 0 OR AuthorUserId = @me)
                    """,
                    [("@id", id), ("@me", userId)],
                    reader => new NoteChange(
                        reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), session.Dialect.ReadDateTime(reader, 3), session.Dialect.ReadDateTime(reader, 4),
                        session.Dialect.ReadDate(reader, 5), session.Dialect.ReadDate(reader, 6)),
                    cancellationToken).ConfigureAwait(false);
                return rows.FirstOrDefault();
            },
            cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// ユーザーには依らない
    /// 読み方は MmmBatch と共通の <see cref="SqlServerNoteChangeReader"/> に持つ
    /// </remarks>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// 設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public override Task<NoteChanges> GetChangesAsync(DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken = default)
        => new SqlServerNoteChangeReader(database).ReadAsync(since, today, cancellationToken);
}
