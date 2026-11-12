using System.Text.Json;
using Microsoft.Data.Sqlite;
using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.Data.Notes;
using MmmTool.Notes.Core;

namespace MmmTool.Data.Sqlite.Notes;

/// <summary>ノートをローカルの SQLiteに保存する</summary>
/// <param name="database">データベースファイルを開く入口</param>
/// <param name="time">更新日時を書くための時計 (SQLite にはサーバーの時計が無いので、アプリが書く)。</param>
/// <remarks>
/// SQL Server の保存先と同じ設計の表と、同じ読み書きの形で、docs/specs/notes.md にある。
/// ユーザーは <c>Id = 1</c> の固定値で、<c>AppUser</c> の表は持たない。
/// 書き込みは書き込みの許可を最初に取るトランザクションの中で行う。
/// 処理は UI スレッドを止めないよう、別のスレッドで行う。
/// SQL はパラメーター化だけで書く。
/// </remarks>
public sealed class SqliteNoteRepository(SqliteDatabase database, TimeProvider time) : INoteRepository
{
    /// <summary>ローカルのユーザーの番号</summary>
    private const int LocalUserId = 1;

    /// <summary>表の全部の列</summary>
    private const string Columns = "Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, AuthorUserId, IsPrivate, StartDate, EndDate, EventDate, Priority, Title, Body";

    /// <inheritdoc />
    public bool IsShared => false;

    /// <inheritdoc />
    public int CurrentUserId => LocalUserId;

    /// <inheritdoc />
    /// <remarks>ローカルモードは通知が無いので、常に null を返す。</remarks>
    public Task<NoteChange?> FindAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult<NoteChange?>(null);

    /// <inheritdoc />
    /// <remarks>ローカルモードはほかの人の更新が無いので、常に空を返す。</remarks>
    public Task<NoteChanges> GetChangesAsync(DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken = default)
        => Task.FromResult(new NoteChanges(time.GetUtcNow(), []));

    /// <inheritdoc />
    /// <remarks>ローカルモードはユーザーの表が無いので、名前は空文字を返す。</remarks>
    public Task<IReadOnlyList<NoteWithNames>> GetOnDayAsync(DateOnly day, bool includeDeleted, CancellationToken cancellationToken = default)
        => ReadWithNamesAsync(
            "EndDate >= @day AND StartDate <= @day",
            [("@day", day), ("@includeDeleted", includeDeleted), ("@me", LocalUserId)],
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 更新日時はいつも同じ時差の同じ形の文字で保存するので、範囲の端も同じ形の文字にして、文字の大小で比べる。
    /// ローカルモードはユーザーの表が無いので、名前は空文字を返す。
    /// </remarks>
    public Task<IReadOnlyList<NoteWithNames>> GetUpdatedBetweenAsync(DateTimeOffset from, DateTimeOffset to, bool includeDeleted, CancellationToken cancellationToken = default)
        => ReadWithNamesAsync(
            "UpdatedAt >= @from AND UpdatedAt < @to",
            [("@from", from), ("@to", to), ("@includeDeleted", includeDeleted), ("@me", LocalUserId)],
            cancellationToken);

    /// <summary>条件に合う見えるノートを読む</summary>
    /// <param name="condition">
    /// 絞り込みの条件
    /// 定数だけを渡す (SQL に埋め込むため)
    /// </param>
    /// <param name="parameters">
    /// パラメーター
    /// <c>@includeDeleted</c> と <c>@me</c> を含める
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>ノートと名前で、並びは不定。</returns>
    private Task<IReadOnlyList<NoteWithNames>> ReadWithNamesAsync(string condition, (string Name, object Value)[] parameters, CancellationToken cancellationToken)
        => RunAsync<IReadOnlyList<NoteWithNames>>(
            async connection =>
            {
                var rows = await SqliteQueries.QueryAsync(
                    connection,
                    null,
                    $"SELECT {Columns} FROM Note WHERE {condition} AND (@includeDeleted = 1 OR IsDeleted = 0) AND (IsPrivate = 0 OR AuthorUserId = @me)",
                    parameters,
                    ReadRow,
                    cancellationToken).ConfigureAwait(false);

                // ほかの実施日は、読んだノートの番号の分を、1 回の問い合わせでまとめて読む。SQL Server と同じ形
                var dates = await ReadOtherEventDatesAsync(connection, [.. rows.Select(row => row.Id)], cancellationToken).ConfigureAwait(false);
                return [.. rows.Select(row => new NoteWithNames(row.ToNote(DatesOf(dates, row.Id)), "", ""))];
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Note>> GetOnEventDateAsync(DateOnly day, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<Note>>(
            async connection =>
            {
                // 実施日か、ほかの実施日のどちらかがその日のノート。番号を UNION で集める。SQL Server と同じ形で、枝ごとに索引が使える
                var rows = await SqliteQueries.QueryAsync(
                    connection,
                    null,
                    $"SELECT {Columns} FROM Note WHERE Id IN (SELECT Id FROM Note WHERE EventDate = @day UNION SELECT NoteId FROM NoteAttribute WHERE CategoryNo = @category AND Value = @dayText AND IsDeleted = 0) AND IsDeleted = 0 AND (IsPrivate = 0 OR AuthorUserId = @me)",
                    [("@day", day), ("@category", (long)NoteAttributeKind.OtherEventDate), ("@dayText", NoteOtherEventDates.ToValue(day)), ("@me", LocalUserId)],
                    ReadRow,
                    cancellationToken).ConfigureAwait(false);
                var dates = await ReadOtherEventDatesAsync(connection, [.. rows.Select(row => row.Id)], cancellationToken).ConfigureAwait(false);
                return (IReadOnlyList<Note>)[.. rows.Select(row => row.ToNote(DatesOf(dates, row.Id)))];
            },
            cancellationToken);

    /// <summary>ノートの番号の分のほかの実施日を 1 回の問い合わせでまとめて読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="noteIds">ノートの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// ノートの番号ごとの、日付の順の一覧。
    /// ほかの実施日が無いノートは含まない。
    /// </returns>
    private static async Task<Dictionary<int, IReadOnlyList<DateOnly>>> ReadOtherEventDatesAsync(SqliteConnection connection, IReadOnlyCollection<int> noteIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, IReadOnlyList<DateOnly>>();
        if (noteIds.Count == 0)
        {
            return result;
        }

        // 番号は json_each で渡す。引数を 1 つずつ並べると、1 回の問い合わせで渡せる数を超えうるため
        var rows = await SqliteQueries.QueryAsync(
            connection,
            null,
            "SELECT NoteId, Value FROM NoteAttribute WHERE CategoryNo = @category AND IsDeleted = 0 AND NoteId IN (SELECT value FROM json_each(@ids))",
            [("@category", (long)NoteAttributeKind.OtherEventDate), ("@ids", JsonSerializer.Serialize(noteIds))],
            reader => (NoteId: reader.GetInt32(0), Value: reader.GetString(1)),
            cancellationToken).ConfigureAwait(false);

        foreach (var group in rows.GroupBy(row => row.NoteId))
        {
            result[group.Key] = NoteOtherEventDates.ToDates(group.Select(row => row.Value));
        }
        return result;
    }

    /// <summary>枝番と日付の組を json_each で渡す JSON にする</summary>
    /// <param name="items">枝番と日付の組</param>
    /// <returns>JSON の文字</returns>
    private static string ToJson(IReadOnlyList<(int CodeNo, DateOnly Day)> items)
        => JsonSerializer.Serialize(items.Select(item => new { codeNo = item.CodeNo, value = NoteOtherEventDates.ToValue(item.Day) }));

    /// <summary>ノートの番号に合うほかの実施日を取り出す</summary>
    /// <param name="dates"><see cref="ReadOtherEventDatesAsync"/> の結果</param>
    /// <param name="noteId">ノートの番号</param>
    /// <returns>
    /// 日付の順の一覧
    /// 無ければ空
    /// </returns>
    private static IReadOnlyList<DateOnly> DatesOf(Dictionary<int, IReadOnlyList<DateOnly>> dates, int noteId)
        => dates.TryGetValue(noteId, out var list) ? list : [];

    /// <summary>ノートのほかの実施日を新しい日付の一覧に合わせて書く</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">ノートを書いているトランザクション</param>
    /// <param name="noteId">ノートの番号</param>
    /// <param name="dates">保存したい日付の一覧</param>
    /// <param name="now">更新日時</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き終わったことを表すタスク</returns>
    /// <remarks>
    /// 保存済みの行を、削除済みも含めて 1 回で読み、差を計算して、書き込みを 1 回の往復でまとめる。
    /// SQL Server と同じ形。
    /// 日付を日付の順に並べて、枝番 1 から順に書き直す。
    /// 削除済みの行も使い回し、日付の数より後ろの行は論理削除する。
    /// </remarks>
    private static async Task WriteOtherEventDatesAsync(SqliteConnection connection, SqliteTransaction transaction, int noteId, IReadOnlyList<DateOnly> dates, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await SqliteQueries.QueryAsync(
            connection,
            transaction,
            "SELECT CodeNo, Value, IsDeleted FROM NoteAttribute WHERE NoteId = @noteId AND CategoryNo = @category",
            [("@noteId", (long)noteId), ("@category", (long)NoteAttributeKind.OtherEventDate)],
            reader => new NoteAttributeRow(reader.GetInt32(0), reader.GetString(1), reader.GetInt64(2) != 0),
            cancellationToken).ConfigureAwait(false);

        var changes = NoteOtherEventDates.Diff([.. existing], dates);
        if (changes.IsEmpty)
        {
            return;
        }

        await SqliteQueries.ExecuteAsync(
            connection,
            transaction,
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
            """,
            [
                ("@now", now),
                ("@user", LocalUserId),
                ("@noteId", (long)noteId),
                ("@category", (long)NoteAttributeKind.OtherEventDate),
                ("@delete", JsonSerializer.Serialize(changes.Delete)),
                ("@update", ToJson(changes.Update)),
                ("@insert", ToJson(changes.Insert)),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">入力内容が正しくない</exception>
    public async Task<Note> AddAsync(NoteInput input, CancellationToken cancellationToken = default)
    {
        NoteRow.Validate(input);

        var now = time.GetUtcNow();
        Note? saved = null;
        await RunInTransactionAsync(
            async (connection, transaction) =>
            {
                // 採番された番号は、RETURNING で同じ往復の中で受け取る。書いたあとに別に問い合わせない
                var id = Convert.ToInt32(await SqliteQueries.ScalarAsync(
                    connection,
                    transaction,
                    "INSERT INTO Note (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, AuthorUserId, IsPrivate, StartDate, EndDate, EventDate, Priority, Title, Body) VALUES (0, @now, @user, @now, @user, @user, @private, @start, @end, @eventDate, @priority, @title, @body) RETURNING Id",
                    [("@now", now), ("@user", LocalUserId), ("@private", false), ("@start", input.StartDate), ("@end", input.EndDate), ("@eventDate", input.EventDate), ("@priority", (long)input.Priority), ("@title", input.Title), ("@body", input.Body)],
                    cancellationToken).ConfigureAwait(false));
                await WriteOtherEventDatesAsync(connection, transaction, id, input.OtherEventDates, now, cancellationToken).ConfigureAwait(false);
                saved = new Note
                {
                    Id = id,
                    AuthorUserId = LocalUserId,
                    StartDate = input.StartDate,
                    EndDate = input.EndDate,
                    EventDate = input.EventDate,
                    OtherEventDates = [.. input.OtherEventDates.Order()],
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
    /// <exception cref="ArgumentException">入力内容が正しくない</exception>
    public async Task<bool> UpdateAsync(int id, NoteInput input, CancellationToken cancellationToken = default)
    {
        NoteRow.Validate(input);

        var changed = 0;
        var now = time.GetUtcNow();
        await RunInTransactionAsync(
            async (connection, transaction) =>
            {
                // ノートの更新日時と更新者は、ほかの実施日だけを直したときも動かす。SQL Server と同じ形
                changed = await SqliteQueries.ExecuteAsync(
                    connection,
                    transaction,
                    "UPDATE Note SET StartDate = @start, EndDate = @end, EventDate = @eventDate, Priority = @priority, Title = @title, Body = @body, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id",
                    [("@start", input.StartDate), ("@end", input.EndDate), ("@eventDate", input.EventDate), ("@priority", (long)input.Priority), ("@title", input.Title), ("@body", input.Body), ("@now", now), ("@user", LocalUserId), ("@id", id)],
                    cancellationToken).ConfigureAwait(false);
                if (changed > 0)
                {
                    await WriteOtherEventDatesAsync(connection, transaction, id, input.OtherEventDates, now, cancellationToken).ConfigureAwait(false);
                }
            },
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

    /// <summary>接続を開いて読む処理を別のスレッドで実行する</summary>
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

    /// <summary>ノートの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    /// <remarks><see cref="Columns"/> の順</remarks>
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
