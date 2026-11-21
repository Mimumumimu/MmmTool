using System.Data.Common;
using MmmTool.Data.Queries;
using MmmTool.Notes.Core;

namespace MmmTool.Data.Notes;

/// <summary>ノートの Repository の共通の基底</summary>
/// <param name="gateway">接続を開いて処理を実行する入口</param>
/// <remarks>
/// SQLite と SQL Server で同じ設計の表を、同じ読み書きの流れで扱う。
/// 処理の流れは、ここに 1 つだけ書く。
/// 流れとは、どの表を、どの順に、何回の往復で読み書きするかのこと。
/// DB の種類ごとのプロジェクトに残すのは、種類ごとに書き方が違う SQL と、名前の引き方と、他の人の更新の読み方だけ。
/// 読み書きの SQL はすべて「見える」条件 (全員に見えるものか、自分が書いたもの)を持つ。
/// 見えないノートを番号だけで操作させないため。
/// 「自分だけ」の切り替えは書いた人だけができる。
/// ほかの人が全員に見えるノートを、自分だけのものにして隠さないため。
/// </remarks>
public abstract class NoteRepository(DbGateway gateway) : INoteRepository
{
    /// <summary>表の全部の列</summary>
    private const string Columns = "Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, AuthorUserId, IsPrivate, StartDate, EndDate, EventDate, Priority, Title, Body";

    /// <summary>接続を開いて処理を実行する入口</summary>
    protected DbGateway Gateway => gateway;

    /// <inheritdoc />
    public abstract bool IsShared { get; }

    /// <inheritdoc />
    public abstract int CurrentUserId { get; }

    /// <summary>書いた人と更新した人の名前を読む列</summary>
    /// <remarks>
    /// ノートの表の別名 <c>n</c> を使う。
    /// 名前の 2 列を、この順に返す。
    /// 表示のための名前で、検索や索引には使わない。
    /// </remarks>
    protected abstract string NameColumns { get; }

    /// <summary>名前を引くための結合</summary>
    /// <remarks>
    /// ノートの表の別名 <c>n</c> に続けて書く
    /// 名前を引かない種類は空
    /// </remarks>
    protected abstract string NameJoins { get; }

    /// <summary>NoteAttribute の行を書きたい内容に合わせて書く SQL</summary>
    /// <remarks>
    /// 3 つの文 (更新・削除・挿入)を 1 回の往復で行う。
    /// パラメーターは <c>@noteId</c> と、JSON の <c>@update</c>・<c>@delete</c>・<c>@insert</c>。
    /// JSON の要素は <c>categoryNo</c>・<c>branchNo</c>・<c>codeNo</c> で、更新と挿入は <c>value</c> も持つ。
    /// </remarks>
    protected abstract string WriteAttributesSql { get; }

    /// <inheritdoc />
    public abstract Task<NoteChange?> FindAsync(int id, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<NoteChanges> GetChangesAsync(DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public Task<IReadOnlyList<NoteWithNames>> GetOnDayAsync(DateOnly day, bool includeDeleted, CancellationToken cancellationToken = default)
        => ReadWithNamesAsync("n.EndDate >= @day AND n.StartDate <= @day", [("@day", day)], includeDeleted, cancellationToken);

    /// <inheritdoc />
    /// <remarks>更新日時の索引 <c>IX_Note_UpdatedAt</c> で範囲を絞る</remarks>
    public Task<IReadOnlyList<NoteWithNames>> GetUpdatedBetweenAsync(DateTimeOffset from, DateTimeOffset to, bool includeDeleted, CancellationToken cancellationToken = default)
        => ReadWithNamesAsync("n.UpdatedAt >= @from AND n.UpdatedAt < @to", [("@from", from), ("@to", to)], includeDeleted, cancellationToken);

    /// <summary>条件に合う見えるノートを書いた人と更新した人の名前つきで 1 回の問い合わせで読む</summary>
    /// <param name="condition">
    /// ノートの表の別名 <c>n</c> を使った、絞り込みの条件。
    /// 定数だけを渡す (SQL に埋め込むため)。
    /// </param>
    /// <param name="parameters">条件で使うパラメーター</param>
    /// <param name="includeDeleted">削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>ノートと名前で、並びは不定。</returns>
    private Task<IReadOnlyList<NoteWithNames>> ReadWithNamesAsync(string condition, (string, object)[] parameters, bool includeDeleted, CancellationToken cancellationToken)
    {
        var userId = gateway.RequireUserId();
        return gateway.ReadAsync<IReadOnlyList<NoteWithNames>>(
            async session =>
            {
                var d = session.Dialect;
                var rows = await session.QueryAsync(
                    $"""
                    SELECT {Qualified("n")}{NameColumns}
                    FROM {d.Table("Note")} AS n{NameJoins}
                    WHERE {condition}
                      AND (@includeDeleted = 1 OR n.IsDeleted = 0)
                      AND (n.IsPrivate = 0 OR n.AuthorUserId = @me)
                    """,
                    [.. parameters, ("@includeDeleted", includeDeleted), ("@me", userId)],
                    reader => (Row: ReadRow(reader), AuthorName: reader.GetString(14), UpdatedByName: reader.GetString(15)),
                    cancellationToken).ConfigureAwait(false);

                // 種類と日程は、読んだノートの番号の分を、1 回の問い合わせでまとめて読む (本文を、行の数だけ運ばないため、結合しない)
                var attributes = await ReadAttributesAsync(session, [.. rows.Select(item => item.Row.Id)], cancellationToken).ConfigureAwait(false);
                return [.. rows.Select(item => new NoteWithNames(item.Row.ToNote(AttributesOf(attributes, item.Row.Id)), item.AuthorName, item.UpdatedByName))];
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Note>> GetOnEventDateAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var userId = gateway.RequireUserId();
        return gateway.ReadAsync<IReadOnlyList<Note>>(
            async session =>
            {
                var d = session.Dialect;

                // 実施日か、ほかの実施日のどちらかがその日のノート。番号を UNION で集める。枝ごとに、IX_Note_EventDate と IX_NoteAttribute_CategoryNo_CodeNo_Value_NoteId の索引が使えるため (OR で 2 つの表をまたぐと、ノートの表を走査しうる)
                var rows = await session.QueryAsync(
                    $"""
                    SELECT {Columns}
                    FROM {d.Table("Note")}
                    WHERE Id IN (SELECT Id FROM {d.Table("Note")} WHERE EventDate = @day
                                 UNION
                                 SELECT NoteId FROM {d.Table("NoteAttribute")} WHERE CategoryNo = @category AND CodeNo = @item AND Value = @dayText AND IsDeleted = 0)
                      AND IsDeleted = 0
                      AND (IsPrivate = 0 OR AuthorUserId = @me)
                    """,
                    [("@day", day), ("@category", NoteAttributeKind.Schedule), ("@item", NoteAttributeItem.Date), ("@dayText", NoteAttributes.ToValue(day)), ("@me", userId)],
                    ReadRow,
                    cancellationToken).ConfigureAwait(false);
                var attributes = await ReadAttributesAsync(session, [.. rows.Select(row => row.Id)], cancellationToken).ConfigureAwait(false);
                return [.. rows.Select(row => row.ToNote(AttributesOf(attributes, row.Id)))];
            },
            cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">入力内容が正しくない</exception>
    public async Task<Note> AddAsync(NoteInput input, CancellationToken cancellationToken = default)
    {
        NoteRow.Validate(input);
        Note? saved = null;

        // ノートと種類・日程は、1 つのトランザクションで書く。途中で止まっても、日程だけが抜けたノートが残らないため
        await gateway.WriteAsync(
            async session =>
            {
                var d = session.Dialect;

                // 採番された番号と更新日時は、同じ往復の中で受け取る。書いたあとに別に問い合わせない
                var created = (await session.QueryAsync(
                    d.InsertReturning("Note", "AuthorUserId, IsPrivate, StartDate, EndDate, EventDate, Priority, Title, Body", "@user, @isPrivate, @start, @end, @eventDate, @priority, @title, @body", "Id, UpdatedAt"),
                    InputParameters(input),
                    reader => (Id: reader.GetInt32(0), UpdatedAt: d.ReadDateTime(reader, 1)),
                    cancellationToken).ConfigureAwait(false))[0];
                await WriteAttributesAsync(session, created.Id, input, cancellationToken).ConfigureAwait(false);
                var items = ItemsOf(input);
                saved = new Note
                {
                    Id = created.Id,
                    AuthorUserId = session.UserId,
                    IsPrivate = input.IsPrivate,
                    StartDate = input.StartDate,
                    EndDate = input.EndDate,
                    EventDate = input.EventDate,
                    Kind = input.Kind,
                    Schedule = input.Kind == NoteKind.Schedule ? items : [],
                    OtherEventDates = [.. items.Select(item => item.Date).Distinct().Order()],
                    Priority = input.Priority,
                    Title = input.Title,
                    Body = input.Body,
                    UpdatedAt = created.UpdatedAt,
                    UpdatedByUserId = session.UserId,
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
        var kindChanged = false;

        // ノートの更新日時と更新者は、日程だけを直したときも動く。他の人の更新の通知が、日付だけの変更も拾うため
        await gateway.WriteAsync(
            async session =>
            {
                var d = session.Dialect;

                // 種類は作ったあとに変えられない。見えるノートの今の種類を、書く前に、同じトランザクションの中で読む
                var currentKinds = await session.QueryAsync(
                    $"""
                    SELECT COALESCE((SELECT a.Value FROM {d.Table("NoteAttribute")} AS a WHERE a.NoteId = n.Id AND a.CategoryNo = @partCategory AND a.CodeNo = @partItem AND a.IsDeleted = 0), '')
                    FROM {d.Table("Note")} AS n
                    WHERE n.Id = @id AND (n.IsPrivate = 0 OR n.AuthorUserId = @user)
                    """,
                    [("@id", id), ("@partCategory", NoteAttributeKind.UsedPart), ("@partItem", NoteAttributeItem.Part)],
                    reader => reader.GetString(0),
                    cancellationToken).ConfigureAwait(false);
                if (currentKinds.Count > 0 && KindOf(currentKinds[0]) != input.Kind)
                {
                    kindChanged = true;
                    return;
                }

                changed = await session.ExecuteAsync(
                    $"""
                    UPDATE {d.Table("Note")}
                    SET IsPrivate = CASE WHEN AuthorUserId = @user THEN @isPrivate ELSE IsPrivate END,
                        StartDate = @start, EndDate = @end, EventDate = @eventDate, Priority = @priority, Title = @title, Body = @body,
                        UpdatedAt = {d.Now}, UpdatedByUserId = @user
                    WHERE Id = @id AND (IsPrivate = 0 OR AuthorUserId = @user)
                    """,
                    [.. InputParameters(input), ("@id", id)],
                    cancellationToken).ConfigureAwait(false);
                if (changed > 0)
                {
                    await WriteAttributesAsync(session, id, input, cancellationToken).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);
        if (kindChanged)
        {
            throw new ArgumentException("種類は変えられません。", nameof(input));
        }
        return changed > 0;
    }

    /// <inheritdoc />
    public async Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default)
    {
        var changed = 0;
        await gateway.WriteAsync(
            async session => changed = await session.ExecuteAsync(
                $"UPDATE {session.Dialect.Table("Note")} SET IsDeleted = @isDeleted, UpdatedAt = {session.Dialect.Now}, UpdatedByUserId = @user WHERE Id = @id AND IsDeleted <> @isDeleted AND (IsPrivate = 0 OR AuthorUserId = @user)",
                [("@isDeleted", isDeleted), ("@id", id)],
                cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        return changed > 0;
    }

    /// <summary>ノートの番号の分の種類と日程を 1 回の問い合わせでまとめて読む</summary>
    /// <param name="session">開いた接続</param>
    /// <param name="noteIds">ノートの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// ノートの番号ごとの、種類と日程。
    /// 種類も日程も無いノートは含まない。
    /// </returns>
    private static async Task<Dictionary<int, NoteAttributeData>> ReadAttributesAsync(DbSession session, IReadOnlyCollection<int> noteIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, NoteAttributeData>();
        if (noteIds.Count == 0)
        {
            return result;
        }

        // 番号は JSON の 1 つのパラメーターで渡す。引数を 1 つずつ並べると、1 回の問い合わせで渡せる数 (SQL Server は 2100 個)を超えうるため
        var d = session.Dialect;
        var rows = await session.QueryAsync(
            $"SELECT NoteId, CategoryNo, BranchNo, CodeNo, Value FROM {d.Table("NoteAttribute")} WHERE CategoryNo IN (@schedule, @usedPart) AND IsDeleted = 0 AND NoteId IN ({d.IdSelect("@ids")})",
            [("@schedule", NoteAttributeKind.Schedule), ("@usedPart", NoteAttributeKind.UsedPart), ("@ids", IdJson.From(noteIds))],
            reader => (NoteId: reader.GetInt32(0), Key: new NoteAttributeKey(reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)), Value: reader.GetString(4)),
            cancellationToken).ConfigureAwait(false);

        foreach (var group in rows.GroupBy(row => row.NoteId))
        {
            result[group.Key] = NoteAttributes.Read(group.Select(row => (row.Key, row.Value)));
        }
        return result;
    }

    /// <summary>ノートの番号に合う種類と日程を取り出す</summary>
    /// <param name="attributes"><see cref="ReadAttributesAsync"/> の結果</param>
    /// <param name="noteId">ノートの番号</param>
    /// <returns>種類と日程で、無ければ通常で日程なし。</returns>
    private static NoteAttributeData AttributesOf(Dictionary<int, NoteAttributeData> attributes, int noteId)
        => attributes.TryGetValue(noteId, out var data) ? data : new NoteAttributeData(NoteKind.Normal, []);

    /// <summary>保存したい日程の行にする</summary>
    /// <param name="input">入力内容で、保存する形に直したもの。</param>
    /// <returns>種類が日程なら日程の行で、通常なら、ほかの実施日を日付だけの行にしたもの。</returns>
    private static IReadOnlyList<ScheduleItem> ItemsOf(NoteInput input)
        => input.Kind == NoteKind.Schedule ? ScheduleItem.Sort(input.ScheduleItems) : [.. input.OtherEventDates.Distinct().Order().Select(ScheduleItem.OfDate)];

    /// <summary>種類の値の文字を種類にする</summary>
    /// <param name="value">使う部品の行の値で、行が無ければ空文字。</param>
    /// <returns>種類で、読めないときは通常。</returns>
    private static NoteKind KindOf(string value)
        => int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && Enum.IsDefined((NoteKind)number)
            ? (NoteKind)number
            : NoteKind.Normal;

    /// <summary>書き込む行を JSON にする</summary>
    /// <param name="rows">書き込む行</param>
    /// <returns>JSON の文字</returns>
    private static string ToJson(IEnumerable<NoteAttributeValue> rows)
        => System.Text.Json.JsonSerializer.Serialize(rows.Select(row => new { categoryNo = row.Key.CategoryNo, branchNo = row.Key.BranchNo, codeNo = row.Key.CodeNo, value = row.Value }));

    /// <summary>削除する行の位置を JSON にする</summary>
    /// <param name="keys">削除する行の位置</param>
    /// <returns>JSON の文字</returns>
    private static string ToJson(IEnumerable<NoteAttributeKey> keys)
        => System.Text.Json.JsonSerializer.Serialize(keys.Select(key => new { categoryNo = key.CategoryNo, branchNo = key.BranchNo, codeNo = key.CodeNo }));

    /// <summary>ノートの種類と日程を、入力内容に合わせて書く</summary>
    /// <param name="session">ノートを書いているトランザクションの接続</param>
    /// <param name="noteId">ノートの番号</param>
    /// <param name="input">保存したい内容で、保存する形に直したもの。</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き終わったことを表すタスク</returns>
    /// <remarks>
    /// 保存済みの行を、削除済みも含めて 1 回で読み、差を計算して、書き込みを 1 回の往復でまとめる。
    /// 行ごとに問い合わせない。
    /// 日程は日付と開始の順に並べて、枝番 1 から順に書き直す。
    /// 削除済みの行も使い回し、数より後ろの行は論理削除する。
    /// </remarks>
    private async Task WriteAttributesAsync(DbSession session, int noteId, NoteInput input, CancellationToken cancellationToken)
    {
        var existing = await session.QueryAsync(
            $"SELECT CategoryNo, BranchNo, CodeNo, Value, IsDeleted FROM {session.Dialect.Table("NoteAttribute")} WHERE NoteId = @noteId AND CategoryNo IN (@schedule, @usedPart)",
            [("@noteId", noteId), ("@schedule", NoteAttributeKind.Schedule), ("@usedPart", NoteAttributeKind.UsedPart)],
            reader => new NoteAttributeRow(new NoteAttributeKey(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)), reader.GetString(3), session.Dialect.ReadBool(reader, 4)),
            cancellationToken).ConfigureAwait(false);

        var changes = NoteAttributes.Diff([.. existing], NoteAttributes.Desired(input.Kind, ItemsOf(input)));
        if (changes.IsEmpty)
        {
            return;
        }

        await session.ExecuteAsync(
            WriteAttributesSql,
            [
                ("@noteId", noteId),
                ("@delete", ToJson(changes.Delete)),
                ("@update", ToJson(changes.Update)),
                ("@insert", ToJson(changes.Insert)),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>入力内容のパラメーター</summary>
    /// <param name="input">入力内容</param>
    /// <returns>パラメーターの並び</returns>
    private static (string, object)[] InputParameters(NoteInput input)
        =>
        [
            ("@isPrivate", input.IsPrivate),
            ("@start", input.StartDate),
            ("@end", input.EndDate),
            ("@eventDate", input.EventDate),
            ("@priority", (byte)input.Priority),
            ("@title", input.Title),
            ("@body", input.Body),
        ];

    /// <summary>表の全部の列に表の別名を付けたもの</summary>
    /// <param name="alias">
    /// ノートの表の別名
    /// 定数だけを渡す (SQL に埋め込むため)
    /// </param>
    /// <returns>別名つきの列の並び (14 列)</returns>
    private static string Qualified(string alias) => string.Join(", ", Columns.Split(", ").Select(column => $"{alias}.{column}"));

    /// <summary>ノートの行を読む</summary>
    /// <param name="reader">読み取り中の結果</param>
    /// <returns>行</returns>
    /// <remarks><see cref="Columns"/> の順</remarks>
    private NoteRow ReadRow(DbDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        IsDeleted = gateway.Dialect.ReadBool(reader, 1),
        CreatedAt = gateway.Dialect.ReadDateTime(reader, 2),
        CreatedByUserId = reader.GetInt32(3),
        UpdatedAt = gateway.Dialect.ReadDateTime(reader, 4),
        UpdatedByUserId = reader.GetInt32(5),
        AuthorUserId = reader.GetInt32(6),
        IsPrivate = gateway.Dialect.ReadBool(reader, 7),
        StartDate = gateway.Dialect.ReadDate(reader, 8),
        EndDate = gateway.Dialect.ReadDate(reader, 9),
        EventDate = gateway.Dialect.ReadDate(reader, 10),
        Priority = gateway.Dialect.ReadByte(reader, 11),
        Title = reader.GetString(12),
        Body = reader.GetString(13),
    };
}
