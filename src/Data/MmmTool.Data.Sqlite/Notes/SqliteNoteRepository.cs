using Microsoft.Data.Sqlite;
using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.Data.Notes;
using MmmTool.Notes.Core;

namespace MmmTool.Data.Sqlite.Notes;

/// <summary>
/// ノートを、ローカルの SQLite (<c>Data/MmmTool.db</c>の <c>Note</c> 表)に保存する。
/// </summary>
/// <param name="database">データベースファイルを開く入口</param>
/// <param name="time">更新日時を書くための時計 (SQLite にはサーバーの時計が無いので、アプリが書く)</param>
/// <remarks>
/// SQL Server の保存先と同じ設計の表と、同じ読み書きの形 (docs/specs/notes.md)。ユーザーは <c>Id = 1</c> の固定値で、<c>AppUser</c> の表は持たない。
/// 書き込みは、書き込みの許可を最初に取るトランザクションの中で行う。処理は、UI スレッドを止めないよう、別のスレッドで行う。SQL はパラメーター化だけで書く。
/// </remarks>
public sealed class SqliteNoteRepository(SqliteDatabase database, TimeProvider time) : INoteRepository
{
    /// <summary>ローカルのユーザーの番号 (固定)</summary>
    private const int LocalUserId = 1;

    /// <summary>表の全部の列 (読む順)</summary>
    private const string Columns = "Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, AuthorUserId, IsPrivate, StartDate, EndDate, EventDate, Priority, Title, Body";

    /// <inheritdoc />
    public bool IsShared => false;

    /// <inheritdoc />
    public int CurrentUserId => LocalUserId;

    /// <inheritdoc />
    public Task<IReadOnlyList<Note>> GetOnDayAsync(DateOnly day, bool includeDeleted, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<Note>>(
            async connection => [.. (await SqliteQueries.QueryAsync(
                connection,
                null,
                $"SELECT {Columns} FROM Note WHERE EndDate >= @day AND StartDate <= @day AND (@includeDeleted = 1 OR IsDeleted = 0) AND (IsPrivate = 0 OR AuthorUserId = @me)",
                [("@day", day), ("@includeDeleted", includeDeleted), ("@me", LocalUserId)],
                reader => ReadRow(reader).ToNote(),
                cancellationToken).ConfigureAwait(false))],
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Note>> GetOnEventDateAsync(DateOnly day, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<Note>>(
            async connection => [.. (await SqliteQueries.QueryAsync(
                connection,
                null,
                $"SELECT {Columns} FROM Note WHERE EventDate = @day AND IsDeleted = 0 AND (IsPrivate = 0 OR AuthorUserId = @me)",
                [("@day", day), ("@me", LocalUserId)],
                reader => ReadRow(reader).ToNote(),
                cancellationToken).ConfigureAwait(false))],
            cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">入力内容が正しくない。</exception>
    public async Task<Note> AddAsync(NoteInput input, CancellationToken cancellationToken = default)
    {
        NoteRow.Validate(input);

        var now = time.GetUtcNow();
        Note? saved = null;
        await RunInTransactionAsync(
            async (connection, transaction) =>
            {
                await SqliteQueries.ExecuteAsync(
                    connection,
                    transaction,
                    "INSERT INTO Note (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, AuthorUserId, IsPrivate, StartDate, EndDate, EventDate, Priority, Title, Body) VALUES (0, @now, @user, @now, @user, @user, @private, @start, @end, @eventDate, @priority, @title, @body)",
                    [("@now", now), ("@user", LocalUserId), ("@private", false), ("@start", input.StartDate), ("@end", input.EndDate), ("@eventDate", input.EventDate), ("@priority", (long)input.Priority), ("@title", input.Title), ("@body", input.Body)],
                    cancellationToken).ConfigureAwait(false);
                var id = Convert.ToInt32(await SqliteQueries.ScalarAsync(connection, transaction, "SELECT last_insert_rowid()", [], cancellationToken).ConfigureAwait(false));
                saved = new Note
                {
                    Id = id,
                    AuthorUserId = LocalUserId,
                    StartDate = input.StartDate,
                    EndDate = input.EndDate,
                    EventDate = input.EventDate,
                    Priority = input.Priority,
                    Title = input.Title,
                    Body = input.Body,
                    UpdatedAt = now,
                    UpdatedByUserId = LocalUserId,
                };
            },
            cancellationToken).ConfigureAwait(false);
        return saved!;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">入力内容が正しくない。</exception>
    public async Task<bool> UpdateAsync(int id, NoteInput input, CancellationToken cancellationToken = default)
    {
        NoteRow.Validate(input);

        var changed = 0;
        await RunInTransactionAsync(
            async (connection, transaction) => changed = await SqliteQueries.ExecuteAsync(
                connection,
                transaction,
                "UPDATE Note SET StartDate = @start, EndDate = @end, EventDate = @eventDate, Priority = @priority, Title = @title, Body = @body, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id",
                [("@start", input.StartDate), ("@end", input.EndDate), ("@eventDate", input.EventDate), ("@priority", (long)input.Priority), ("@title", input.Title), ("@body", input.Body), ("@now", time.GetUtcNow()), ("@user", LocalUserId), ("@id", id)],
                cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        return changed > 0;
    }

    /// <inheritdoc />
    public async Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default)
    {
        var changed = 0;
        await RunInTransactionAsync(
            async (connection, transaction) => changed = await SqliteQueries.ExecuteAsync(
                connection,
                transaction,
                "UPDATE Note SET IsDeleted = @deleted, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id AND IsDeleted <> @deleted",
                [("@deleted", isDeleted), ("@now", time.GetUtcNow()), ("@user", LocalUserId), ("@id", id)],
                cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        return changed > 0;
    }

    /// <summary>接続を開いて、読む処理を別のスレッドで実行する</summary>
    /// <typeparam name="T">結果の型</typeparam>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の結果</returns>
    private Task<T> RunAsync<T>(Func<SqliteConnection, Task<T>> work, CancellationToken cancellationToken)
        => Task.Run(() => database.RunAsync(work, cancellationToken), cancellationToken);

    /// <summary>書き込みのトランザクションの中で、処理を別のスレッドで実行する</summary>
    /// <param name="work">開いた接続とトランザクションで行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の完了を表すタスク</returns>
    private Task RunInTransactionAsync(Func<SqliteConnection, SqliteTransaction, Task> work, CancellationToken cancellationToken)
        => Task.Run(() => database.RunInTransactionAsync(work, cancellationToken), cancellationToken);

    /// <summary>ノートの行を読む (<see cref="Columns"/> の順)</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static NoteRow ReadRow(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        AuthorUserId = r.GetInt32(6),
        IsPrivate = r.GetInt64(7) != 0,
        StartDate = SqliteValues.ParseDate(r.GetString(8)),
        EndDate = SqliteValues.ParseDate(r.GetString(9)),
        EventDate = SqliteValues.ParseDate(r.GetString(10)),
        Priority = checked((byte)r.GetInt64(11)),
        Title = r.GetString(12),
        Body = r.GetString(13),
    };
}
