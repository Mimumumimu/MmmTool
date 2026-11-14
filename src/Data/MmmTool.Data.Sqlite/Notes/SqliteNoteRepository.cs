using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.Data.Notes;
using MmmTool.Notes.Core;

namespace MmmTool.Data.Sqlite.Notes;

/// <summary>ノートをローカルの SQLiteに保存する</summary>
/// <param name="database">データベースファイルを開く入口</param>
/// <param name="time">更新日時を書くための時計 (SQLite にはサーバーの時計が無いので、アプリが書く)。</param>
/// <remarks>
/// 読み書きの流れは <see cref="NoteRepository"/> にあり、SQL Server の保存先と共通。
/// ここに残すのは、SQLite だけの SQL と、通知が無いこと。
/// ユーザーは <c>Id = 1</c> の固定値で、<c>AppUser</c> の表は持たない。
/// 設計は docs/specs/notes.md にある。
/// </remarks>
public sealed class SqliteNoteRepository(SqliteDatabase database, TimeProvider time)
    : NoteRepository(new SqliteGateway(database, time))
{
    /// <inheritdoc />
    public override bool IsShared => false;

    /// <inheritdoc />
    public override int CurrentUserId => 1;

    /// <inheritdoc />
    /// <remarks>ローカルモードはユーザーの表が無いので、名前は空文字を返す。</remarks>
    protected override string NameColumns => ", '', ''";

    /// <inheritdoc />
    protected override string NameJoins => "";

    /// <inheritdoc />
    protected override string WriteOtherEventDatesSql =>
        """
        UPDATE NoteAttribute
        SET Value = (SELECT json_extract(j.value, '$.value') FROM json_each(@update) AS j WHERE json_extract(j.value, '$.codeNo') = NoteAttribute.CodeNo),
            IsDeleted = 0, UpdatedAt = @now, UpdatedByUserId = @user
        WHERE NoteId = @noteId AND CategoryNo = @category
          AND CodeNo IN (SELECT json_extract(j.value, '$.codeNo') FROM json_each(@update) AS j);

        UPDATE NoteAttribute
        SET IsDeleted = 1, UpdatedAt = @now, UpdatedByUserId = @user
        WHERE NoteId = @noteId AND CategoryNo = @category
          AND CodeNo IN (SELECT value FROM json_each(@delete));

        INSERT INTO NoteAttribute (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, NoteId, CategoryNo, CodeNo, Value)
        SELECT 0, @now, @user, @now, @user, @noteId, @category, json_extract(j.value, '$.codeNo'), json_extract(j.value, '$.value')
        FROM json_each(@insert) AS j;
        """;

    /// <inheritdoc />
    /// <remarks>ローカルモードは通知が無いので、常に null を返す。</remarks>
    public override Task<NoteChange?> FindAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult<NoteChange?>(null);

    /// <inheritdoc />
    /// <remarks>ローカルモードはほかの人の更新が無いので、常に空を返す。</remarks>
    public override Task<NoteChanges> GetChangesAsync(DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken = default)
        => Task.FromResult(new NoteChanges(time.GetUtcNow(), []));
}
