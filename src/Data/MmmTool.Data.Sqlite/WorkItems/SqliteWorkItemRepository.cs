using Microsoft.Data.Sqlite;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.CodeMaster;
using MmmTool.Data.WorkItems;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.Sqlite.WorkItems;

/// <summary>
/// 作業リストを、ローカルの SQLite (<c>Data/MmmTool.db</c>)に保存する。
/// </summary>
/// <param name="database">データベースファイルを開く入口</param>
/// <param name="time">更新日時を書くための時計 (SQLite にはサーバーの時計が無いので、アプリが書く)</param>
/// <remarks>
/// SQL Server の保存先と同じ設計の表と、同じ読み書きの形 (docs/specs/work-items.md)。ユーザーは <c>Id = 1</c> の固定値で、<c>AppUser</c> の表は持たない。
/// 読みは、利用者が選んだ案件で絞って、表ごとの別の <c>SELECT</c> で行う (JOIN で親の列が子の行に重ならない)。木はメモリで組み立てる。
/// 書きは 1 件単位で、更新は変えた列だけ書く。書き込みは、書き込みの許可を最初に取るトランザクション (<c>BEGIN IMMEDIATE</c>)の中で行う。
/// 処理は、UI スレッドを止めないよう、別のスレッドで行う。SQL はパラメーター化だけで書く。
/// </remarks>
public sealed class SqliteWorkItemRepository(SqliteDatabase database, TimeProvider time) : IWorkItemRepository
{
    /// <summary>ローカルのユーザーの番号 (固定)</summary>
    private const int LocalUserId = 1;

    /// <summary>「ない」日付の文字 (<c>9999-12-31</c>)</summary>
    private const string NoDateText = "9999-12-31";

    /// <summary>一度に <c>IN</c> に並べる番号の最大の数</summary>
    private const int ChunkSize = 500;

    /// <inheritdoc />
    public int UserId => LocalUserId;

    /// <inheritdoc />
    public string? RecoveryMessage => database.RecoveryMessage;

    /// <summary>今の日時 (保存先の時計の代わり)</summary>
    private DateTimeOffset Now => time.GetUtcNow();

    /// <inheritdoc />
    public Task<WorkLoadResult> LoadSelectedAsync(CancellationToken cancellationToken = default)
        => RunAsync(connection => ReadContentAsync(connection, null, withMaster: true, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<WorkLoadResult> LoadProjectsAsync(IReadOnlyList<int> projectIds, CancellationToken cancellationToken = default)
        => projectIds.Count == 0
            ? Task.FromResult(new WorkLoadResult([], [], [], [], []))
            : RunAsync(connection => ReadContentAsync(connection, projectIds, withMaster: false, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkRecord>> LoadDayAsync(DateOnly date, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<WorkRecord>>(
            connection => QueryAsync(
                connection,
                null,
                $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM WorkRecord r INNER JOIN WorkProjectSelection s ON s.ProjectId = r.ProjectId AND s.UserId = @user AND s.IsDeleted = 0 WHERE r.Date = @date AND r.IsDeleted = 0",
                [("@user", LocalUserId), ("@date", date)],
                reader => ReadRecord(reader).ToWorkRecord(),
                cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public Task<WorkChanges> GetChangesAsync(DateTimeOffset since, DateOnly date, CancellationToken cancellationToken = default)
        => RunAsync(async connection =>
        {
            // 更新日時のインデックスは、削除された行も含むので、論理削除も差分で届く。選んだ案件で絞る。6 つの問い合わせを 1 回の往復で受け取る
            const string selectionJoin = "INNER JOIN WorkProjectSelection s ON s.ProjectId = {0}.{1} AND s.UserId = @user AND s.IsDeleted = 0";
            (string, object)[] parameters = [("@user", LocalUserId), ("@since", since), ("@date", date), ("@feature", WorkCodes.FeatureNo)];
            var projects = new List<WorkProject>();
            var groups = new List<WorkGroup>();
            var items = new List<WorkItem>();
            var records = new List<WorkRecord>();
            var selections = new List<WorkProjectSelection>();
            var codes = new List<CodeMasterEntry>();
            await QueryBatchAsync(
                connection,
                null,
                [
                    ($"SELECT {Header("p")}, p.Name, p.Note FROM WorkProject p {string.Format(selectionJoin, "p", "Id")} WHERE p.UpdatedAt >= @since", r => projects.Add(ReadProject(r).ToWorkProject())),
                    ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Note FROM WorkGroup g {string.Format(selectionJoin, "g", "ProjectId")} WHERE g.UpdatedAt >= @since", r => groups.Add(ReadGroup(r).ToWorkGroup())),
                    ($"SELECT {Header("i")}, {ItemColumns("i")} FROM WorkItem i {string.Format(selectionJoin, "i", "ProjectId")} WHERE i.UpdatedAt >= @since", r => items.Add(ReadItem(r).ToWorkItem())),
                    ($"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM WorkRecord r {string.Format(selectionJoin, "r", "ProjectId")} WHERE r.UpdatedAt >= @since AND r.Date = @date", r => records.Add(ReadRecord(r).ToWorkRecord())),
                    ($"SELECT {Header("s")}, s.UserId, s.ProjectId FROM WorkProjectSelection s WHERE s.UserId = @user AND s.UpdatedAt >= @since", r => selections.Add(ReadSelection(r).ToWorkProjectSelection())),
                    ($"SELECT {Header("c")}, {CodeColumns("c")} FROM CodeMaster c WHERE c.FeatureNo = @feature AND c.UpdatedAt >= @since", r => codes.Add(ReadCode(r).ToEntry())),
                ],
                parameters,
                cancellationToken);
            return new WorkChanges(projects, groups, items, records, selections, codes);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<WorkProjectEntry>>(
            connection =>
            {
                // 完了済み: 作業が 1 件以上あり、未完了の作業 (状態が完了でない、または完了日が無い)が 1 件も無い
                const string finished = "(EXISTS (SELECT 1 FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0) AND NOT EXISTS (SELECT 1 FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND (w.CompletedOn = @none OR w.StatusCodeNo <> 3)))";
                var sql = $"""
                    SELECT p.Id, p.Name,
                           EXISTS (SELECT 1 FROM WorkProjectSelection s WHERE s.ProjectId = p.Id AND s.UserId = @user AND s.IsDeleted = 0),
                           {finished},
                           CASE WHEN {finished} THEN (SELECT MAX(w.CompletedOn) FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0) ELSE @none END
                    FROM WorkProject p
                    WHERE p.IsDeleted = 0
                      AND (@query = '' OR p.Name LIKE '%' || @query || '%' ESCAPE '\')
                      AND (@activeOnly = 0 OR NOT {finished})
                    ORDER BY CASE WHEN @selectedFirst = 1 AND EXISTS (SELECT 1 FROM WorkProjectSelection s WHERE s.ProjectId = p.Id AND s.UserId = @user AND s.IsDeleted = 0) THEN 0 ELSE 1 END, p.Name, p.Id
                    LIMIT @count OFFSET @offset
                    """;
                return QueryAsync(
                    connection,
                    null,
                    sql,
                    [("@user", LocalUserId), ("@none", NoDateText), ("@query", EscapeLike(query)), ("@activeOnly", activeOnly), ("@selectedFirst", selectedFirst), ("@count", count), ("@offset", offset)],
                    reader => new WorkProjectEntry(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetInt64(2) != 0,
                        reader.GetInt64(3) != 0,
                        reader.GetInt64(3) != 0 ? WorkRowValues.ToDate(SqliteValues.ParseDate(reader.GetString(4))) : null),
                    cancellationToken);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<int> CountRecordsAsync(IReadOnlyList<int> workItemIds, CancellationToken cancellationToken = default)
        => RunAsync(async connection =>
        {
            var total = 0;
            foreach (var chunk in workItemIds.Chunk(ChunkSize))
            {
                total += Convert.ToInt32(await ScalarAsync(
                    connection,
                    null,
                    $"SELECT COUNT(*) FROM WorkRecord WHERE IsDeleted = 0 AND WorkItemId IN ({InList(chunk.Length)})",
                    InParameters(chunk),
                    cancellationToken));
            }
            return total;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<WorkItemDeleteImpact> GetProjectDeleteImpactAsync(int projectId, CancellationToken cancellationToken = default)
        => RunAsync(async connection =>
        {
            (string, object)[] parameters = [("@project", projectId)];
            var groups = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM WorkGroup WHERE ProjectId = @project AND IsDeleted = 0", parameters, cancellationToken);
            var works = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM WorkItem WHERE ProjectId = @project AND IsDeleted = 0", parameters, cancellationToken);
            var records = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM WorkRecord WHERE ProjectId = @project AND IsDeleted = 0", parameters, cancellationToken);
            return new WorkItemDeleteImpact(Convert.ToInt32(groups), Convert.ToInt32(works), Convert.ToInt32(records));
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<WorkProject> AddProjectAsync(string name, CancellationToken cancellationToken = default)
    {
        var row = WorkProjectRow.FromWorkProject(new WorkProject { Name = name });
        var now = Now;
        WorkProject? saved = null;
        await RunInTransactionAsync(async (connection, transaction) =>
        {
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO WorkProject (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, Name, Note) VALUES (0, @now, @user, @now, @user, @name, @note)",
                [("@now", now), ("@user", LocalUserId), ("@name", row.Name), ("@note", row.Note)],
                cancellationToken);
            var id = Convert.ToInt32(await ScalarAsync(connection, transaction, "SELECT last_insert_rowid()", [], cancellationToken));
            await SelectAsync(connection, transaction, id, true, now, cancellationToken);
            saved = (row with { Id = id, CreatedAt = now, UpdatedAt = now, CreatedByUserId = LocalUserId, UpdatedByUserId = LocalUserId }).ToWorkProject();
        }, cancellationToken);
        return saved!;
    }

    /// <inheritdoc />
    public Task UpdateProjectAsync(int projectId, string? name, string? note, CancellationToken cancellationToken = default)
    {
        var sets = new List<string>();
        var parameters = new List<(string, object)>();
        if (name is not null)
        {
            sets.Add("Name = @name");
            parameters.Add(("@name", WorkRowValues.CheckText(name, "案件の名前", WorkRowValues.NameMax, required: true)));
        }
        if (note is not null)
        {
            sets.Add("Note = @note");
            parameters.Add(("@note", WorkRowValues.CheckText(note, "案件の備考", WorkRowValues.NoteMax)));
        }
        if (sets.Count == 0)
        {
            return Task.CompletedTask;
        }

        parameters.AddRange([("@id", projectId), ("@now", Now), ("@user", LocalUserId)]);
        return RunInTransactionAsync(
            (connection, transaction) => ExecuteAsync(
                connection,
                transaction,
                $"UPDATE WorkProject SET {string.Join(", ", sets)}, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id AND IsDeleted = 0",
                [.. parameters],
                cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteProjectAsync(int projectId, CancellationToken cancellationToken = default)
        => RunInTransactionAsync(async (connection, transaction) =>
        {
            (string, object)[] parameters = [("@id", projectId), ("@now", Now), ("@user", LocalUserId)];
            await ExecuteAsync(connection, transaction, "UPDATE WorkProject SET IsDeleted = 1, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id AND IsDeleted = 0", parameters, cancellationToken);
        }, cancellationToken);

    /// <inheritdoc />
    public Task SetProjectSelectedAsync(int projectId, bool selected, CancellationToken cancellationToken = default)
        => RunInTransactionAsync(
            (connection, transaction) => SelectAsync(connection, transaction, projectId, selected, Now, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public async Task<WorkGroup> AddGroupAsync(WorkGroup group, IReadOnlyList<WorkPosition> siblingPositions, CancellationToken cancellationToken = default)
    {
        var row = WorkGroupRow.FromWorkGroup(group);
        var now = Now;
        WorkGroup? saved = null;
        await RunInTransactionAsync(async (connection, transaction) =>
        {
            await ApplyPositionsAsync(connection, transaction, siblingPositions, now, cancellationToken);
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO WorkGroup (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, ParentGroupId, SortOrder, Name, Note) VALUES (0, @now, @user, @now, @user, @project, @parent, @order, @name, @note)",
                [("@now", now), ("@user", LocalUserId), ("@project", row.ProjectId), ("@parent", row.ParentGroupId), ("@order", row.SortOrder), ("@name", row.Name), ("@note", row.Note)],
                cancellationToken);
            var id = Convert.ToInt32(await ScalarAsync(connection, transaction, "SELECT last_insert_rowid()", [], cancellationToken));
            saved = (row with { Id = id, CreatedAt = now, UpdatedAt = now, CreatedByUserId = LocalUserId, UpdatedByUserId = LocalUserId }).ToWorkGroup();
        }, cancellationToken);
        return saved!;
    }

    /// <inheritdoc />
    public async Task<WorkItem> AddItemAsync(WorkItem item, IReadOnlyList<WorkPosition> siblingPositions, CancellationToken cancellationToken = default)
    {
        var row = WorkItemRow.FromWorkItem(item);
        var now = Now;
        WorkItem? saved = null;
        await RunInTransactionAsync(async (connection, transaction) =>
        {
            await ApplyPositionsAsync(connection, transaction, siblingPositions, now, cancellationToken);
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO WorkItem (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, Note,
                                      PriorityCodeNo, StartDate, DueDate, StatusCodeNo, ProgressCodeNo, PlannedHours, CompletedOn, ActualTotalHours)
                VALUES (0, @now, @user, @now, @user, @project, @group, @order, @name, @note, @priority, @start, @due, @status, @progress, @planned, @completed, 0)
                """,
                [
                    ("@now", now), ("@user", LocalUserId), ("@project", row.ProjectId), ("@group", row.GroupId), ("@order", row.SortOrder),
                    ("@name", row.Name), ("@note", row.Note), ("@priority", row.PriorityCodeNo), ("@start", row.StartDate), ("@due", row.DueDate),
                    ("@status", row.StatusCodeNo), ("@progress", row.ProgressCodeNo), ("@planned", row.PlannedHours), ("@completed", row.CompletedOn),
                ],
                cancellationToken);
            var id = Convert.ToInt32(await ScalarAsync(connection, transaction, "SELECT last_insert_rowid()", [], cancellationToken));
            saved = (row with { Id = id, CreatedAt = now, UpdatedAt = now, CreatedByUserId = LocalUserId, UpdatedByUserId = LocalUserId, ActualTotalHours = 0 }).ToWorkItem();
        }, cancellationToken);
        return saved!;
    }

    /// <inheritdoc />
    public Task UpdateGroupAsync(WorkGroup before, WorkGroup after, CancellationToken cancellationToken = default)
    {
        var (b, a) = (WorkGroupRow.FromWorkGroup(before), WorkGroupRow.FromWorkGroup(after));
        var changes = new ColumnChanges();
        changes.Add("Name", b.Name, a.Name);
        changes.Add("Note", b.Note, a.Note);
        return UpdateColumnsAsync("WorkGroup", a.Id, changes, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateItemAsync(WorkItem before, WorkItem after, CancellationToken cancellationToken = default)
    {
        var (b, a) = (WorkItemRow.FromWorkItem(before), WorkItemRow.FromWorkItem(after));
        var changes = new ColumnChanges();
        changes.Add("Name", b.Name, a.Name);
        changes.Add("Note", b.Note, a.Note);
        changes.Add("PriorityCodeNo", b.PriorityCodeNo, a.PriorityCodeNo);
        changes.Add("StartDate", b.StartDate, a.StartDate);
        changes.Add("DueDate", b.DueDate, a.DueDate);
        changes.Add("StatusCodeNo", b.StatusCodeNo, a.StatusCodeNo);
        changes.Add("ProgressCodeNo", b.ProgressCodeNo, a.ProgressCodeNo);
        changes.Add("PlannedHours", b.PlannedHours, a.PlannedHours);
        changes.Add("CompletedOn", b.CompletedOn, a.CompletedOn);
        return UpdateColumnsAsync("WorkItem", a.Id, changes, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdatePositionsAsync(IReadOnlyList<WorkPosition> positions, IReadOnlyList<int> touchedProjectIds, CancellationToken cancellationToken = default)
        => RunInTransactionAsync(async (connection, transaction) =>
        {
            var now = Now;
            await ApplyPositionsAsync(connection, transaction, positions, now, cancellationToken);
            foreach (var projectId in touchedProjectIds)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "UPDATE WorkProject SET UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id",
                    [("@now", now), ("@user", LocalUserId), ("@id", projectId)],
                    cancellationToken);
            }
        }, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(IReadOnlyList<int> groupIds, IReadOnlyList<int> itemIds, CancellationToken cancellationToken = default)
        => RunInTransactionAsync(async (connection, transaction) =>
        {
            var now = Now;
            foreach (var (table, ids) in new[] { ("WorkGroup", groupIds), ("WorkItem", itemIds) })
            {
                foreach (var chunk in ids.Chunk(ChunkSize))
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        $"UPDATE {table} SET IsDeleted = 1, UpdatedAt = @now, UpdatedByUserId = @user WHERE IsDeleted = 0 AND Id IN ({InList(chunk.Length)})",
                        [("@now", now), ("@user", LocalUserId), .. InParameters(chunk)],
                        cancellationToken);
                }
            }
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<WorkRecordResult> SetRecordAsync(WorkRecord record, CancellationToken cancellationToken = default)
    {
        var row = WorkRecordRow.FromWorkRecord(record with { UserId = LocalUserId });
        var now = Now;
        WorkRecordResult? result = null;
        await RunInTransactionAsync(async (connection, transaction) =>
        {
            // 作業の行を先に確かめる (案件の番号は、作業の行が正)。BEGIN IMMEDIATE で書き込みを待ち合わせているので、読んでから書く間に、ほかの書き込みは割り込まない
            var projectValue = await ScalarAsync(connection, transaction, "SELECT ProjectId FROM WorkItem WHERE Id = @item AND IsDeleted = 0", [("@item", row.WorkItemId)], cancellationToken);
            if (projectValue is null)
            {
                throw new DataFileException("この作業は、すでに削除されています。", new InvalidOperationException($"WorkItem {row.WorkItemId} がありません。"));
            }
            var projectId = Convert.ToInt32(projectValue);

            var existing = (await QueryAsync(
                connection,
                transaction,
                "SELECT Id, IsDeleted FROM WorkRecord WHERE WorkItemId = @item AND Date = @date AND CreatedByUserId = @user",
                [("@item", row.WorkItemId), ("@date", row.Date), ("@user", LocalUserId)],
                reader => (Id: reader.GetInt64(0), IsDeleted: reader.GetInt64(1) != 0),
                cancellationToken)).Cast<(long Id, bool IsDeleted)?>().FirstOrDefault();

            if (record.IsEmpty)
            {
                if (existing is { IsDeleted: false } alive)
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        "UPDATE WorkRecord SET IsDeleted = 1, Hours = 0, Note = '', UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id",
                        [("@now", now), ("@user", LocalUserId), ("@id", alive.Id)],
                        cancellationToken);
                }
            }
            else if (existing is { } found)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "UPDATE WorkRecord SET IsDeleted = 0, ProjectId = @project, Hours = @hours, Note = @note, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id",
                    [("@project", projectId), ("@hours", row.Hours), ("@note", row.Note), ("@now", now), ("@user", LocalUserId), ("@id", found.Id)],
                    cancellationToken);
            }
            else
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "INSERT INTO WorkRecord (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, WorkItemId, Date, Hours, Note) VALUES (0, @now, @user, @now, @user, @project, @item, @date, @hours, @note)",
                    [("@now", now), ("@user", LocalUserId), ("@project", projectId), ("@item", row.WorkItemId), ("@date", row.Date), ("@hours", row.Hours), ("@note", row.Note)],
                    cancellationToken);
            }

            // その作業の記録をすべて足し直して、作業の行に書く (差分は足さない)
            var hoursList = await QueryAsync(
                connection,
                transaction,
                "SELECT Hours FROM WorkRecord WHERE WorkItemId = @item AND IsDeleted = 0",
                [("@item", row.WorkItemId)],
                reader => reader.GetDouble(0),
                cancellationToken);
            var total = WorkActualTotal.Calculate(hoursList);
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE WorkItem SET ActualTotalHours = @total, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @item",
                [("@total", (decimal)total), ("@now", now), ("@user", LocalUserId), ("@item", row.WorkItemId)],
                cancellationToken);

            var saved = (await QueryAsync(
                connection,
                transaction,
                $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM WorkRecord r WHERE r.WorkItemId = @item AND r.Date = @date AND r.CreatedByUserId = @user",
                [("@item", row.WorkItemId), ("@date", row.Date), ("@user", LocalUserId)],
                reader => ReadRecord(reader).ToWorkRecord(),
                cancellationToken)).FirstOrDefault();
            result = new WorkRecordResult(saved, total);
        }, cancellationToken);
        return result!;
    }

    /// <summary>1 つの表の、変えた列だけを更新する</summary>
    /// <param name="table">表の名前</param>
    /// <param name="id">行の番号</param>
    /// <param name="changes">変えた列</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新の完了を表すタスク (変えた列が無ければ、何も書かない)</returns>
    private Task UpdateColumnsAsync(string table, int id, ColumnChanges changes, CancellationToken cancellationToken)
    {
        if (changes.IsEmpty)
        {
            return Task.CompletedTask;
        }

        return RunInTransactionAsync(
            (connection, transaction) => ExecuteAsync(
                connection,
                transaction,
                $"UPDATE {table} SET {changes.SetClause}, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id AND IsDeleted = 0",
                [.. changes.Parameters, ("@now", Now), ("@user", LocalUserId), ("@id", id)],
                cancellationToken),
            cancellationToken);
    }

    /// <summary>選びの行を、選ぶ・外す (外したら論理削除し、選び直したら同じ行を使い回す)</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション</param>
    /// <param name="projectId">案件の番号</param>
    /// <param name="selected">選ぶか</param>
    /// <param name="now">今の日時</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    private static async Task SelectAsync(SqliteConnection connection, SqliteTransaction transaction, int projectId, bool selected, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await ScalarAsync(
            connection,
            transaction,
            "SELECT Id FROM WorkProjectSelection WHERE UserId = @user AND ProjectId = @project ORDER BY IsDeleted LIMIT 1",
            [("@user", LocalUserId), ("@project", projectId)],
            cancellationToken);
        if (existing is not null)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE WorkProjectSelection SET IsDeleted = @deleted, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id AND IsDeleted <> @deleted",
                [("@deleted", !selected), ("@now", now), ("@user", LocalUserId), ("@id", Convert.ToInt32(existing))],
                cancellationToken);
        }
        else if (selected)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO WorkProjectSelection (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, UserId, ProjectId) VALUES (0, @now, @user, @now, @user, @user, @project)",
                [("@now", now), ("@user", LocalUserId), ("@project", projectId)],
                cancellationToken);
        }
    }

    /// <summary>行の位置 (案件・親・並び)を書く</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション</param>
    /// <param name="positions">位置</param>
    /// <param name="now">今の日時</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <remarks>作業の案件が変わるときは、その作業の記録の案件の番号も直す。</remarks>
    private static async Task ApplyPositionsAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<WorkPosition> positions, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var p in positions)
        {
            (string, object)[] parameters = [("@project", p.ProjectId), ("@parent", p.ParentId), ("@order", p.SortOrder), ("@now", now), ("@user", LocalUserId), ("@id", p.Id)];
            if (p.Kind == WorkRowKind.Group)
            {
                await ExecuteAsync(connection, transaction, "UPDATE WorkGroup SET ProjectId = @project, ParentGroupId = @parent, SortOrder = @order, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id", parameters, cancellationToken);
            }
            else
            {
                await ExecuteAsync(connection, transaction, "UPDATE WorkItem SET ProjectId = @project, GroupId = @parent, SortOrder = @order, UpdatedAt = @now, UpdatedByUserId = @user WHERE Id = @id", parameters, cancellationToken);
                await ExecuteAsync(
                    connection,
                    transaction,
                    "UPDATE WorkRecord SET ProjectId = @project, UpdatedAt = @now, UpdatedByUserId = @user WHERE WorkItemId = @id AND ProjectId <> @project",
                    parameters,
                    cancellationToken);
            }
        }
    }

    /// <summary>案件の中身 (案件の行・グループ・作業)と、必要なら選んでいる案件の番号・コードマスタを、1 回の往復で読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="projectIds">案件の番号。null なら、自分が選んだ案件</param>
    /// <param name="withMaster">選んでいる案件の番号と、コードマスタも読むか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込んだ内容 (<paramref name="withMaster"/> が false のときは、選んでいる案件の番号とコードマスタは空)</returns>
    private static async Task<WorkLoadResult> ReadContentAsync(SqliteConnection connection, IReadOnlyList<int>? projectIds, bool withMaster, CancellationToken cancellationToken)
    {
        // 選んだ案件で絞るときは、選びの表を案件の番号で結び付けるだけ。番号を指定するときは IN。どちらも返すのは自分の表の列だけ
        string Join(string alias, string column) => projectIds is null
            ? $"INNER JOIN WorkProjectSelection s ON s.ProjectId = {alias}.{column} AND s.UserId = @user AND s.IsDeleted = 0"
            : "";
        string Where(string alias, string column) => projectIds is null
            ? $"{alias}.IsDeleted = 0"
            : $"{alias}.IsDeleted = 0 AND {alias}.{column} IN ({InList(projectIds.Count)})";
        (string, object)[] parameters = projectIds is null ? [("@user", LocalUserId), ("@feature", WorkCodes.FeatureNo)] : [.. InParameters(projectIds), ("@feature", WorkCodes.FeatureNo)];

        var projects = new List<WorkProject>();
        var groups = new List<WorkGroup>();
        var items = new List<WorkItem>();
        var selected = new List<int>();
        var codes = new List<CodeMasterEntry>();
        var queries = new List<(string Sql, Action<SqliteDataReader> Map)>
        {
            ($"SELECT {Header("p")}, p.Name, p.Note FROM WorkProject p {Join("p", "Id")} WHERE {Where("p", "Id")}", r => projects.Add(ReadProject(r).ToWorkProject())),
            ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Note FROM WorkGroup g {Join("g", "ProjectId")} WHERE {Where("g", "ProjectId")}", r => groups.Add(ReadGroup(r).ToWorkGroup())),
            ($"SELECT {Header("i")}, {ItemColumns("i")} FROM WorkItem i {Join("i", "ProjectId")} WHERE {Where("i", "ProjectId")}", r => items.Add(ReadItem(r).ToWorkItem())),
        };
        if (withMaster)
        {
            queries.Add(("SELECT s.ProjectId FROM WorkProjectSelection s INNER JOIN WorkProject p ON p.Id = s.ProjectId AND p.IsDeleted = 0 WHERE s.UserId = @user AND s.IsDeleted = 0", r => selected.Add(r.GetInt32(0))));
            queries.Add(($"SELECT {Header("c")}, {CodeColumns("c")} FROM CodeMaster c WHERE c.FeatureNo = @feature", r => codes.Add(ReadCode(r).ToEntry())));
        }

        await QueryBatchAsync(connection, null, [.. queries], parameters, cancellationToken);
        return new WorkLoadResult(projects, groups, items, selected, codes);
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

    /// <summary>共通ヘッダー (番号・論理削除・作成と更新の日時と人)の列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (6 列)</returns>
    private static string Header(string alias)
        => $"{alias}.Id, {alias}.IsDeleted, {alias}.CreatedAt, {alias}.CreatedByUserId, {alias}.UpdatedAt, {alias}.UpdatedByUserId";

    /// <summary>作業の中身の列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (13 列)</returns>
    private static string ItemColumns(string alias)
        => $"{alias}.ProjectId, {alias}.GroupId, {alias}.SortOrder, {alias}.Name, {alias}.Note, {alias}.PriorityCodeNo, {alias}.StartDate, {alias}.DueDate, {alias}.StatusCodeNo, {alias}.ProgressCodeNo, {alias}.PlannedHours, {alias}.CompletedOn, {alias}.ActualTotalHours";

    /// <summary>コードマスタの中身の列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (6 列)</returns>
    private static string CodeColumns(string alias)
        => $"{alias}.FeatureNo, {alias}.CategoryNo, {alias}.CodeNo, {alias}.SortOrder, {alias}.NumericValue, {alias}.Name";

    /// <summary>案件の行を読む (共通ヘッダー 6 列のあとに、名前・備考)</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkProjectRow ReadProject(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        Name = r.GetString(6),
        Note = r.GetString(7),
    };

    /// <summary>グループの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkGroupRow ReadGroup(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        ProjectId = r.GetInt32(6),
        ParentGroupId = r.GetInt32(7),
        SortOrder = r.GetInt32(8),
        Name = r.GetString(9),
        Note = r.GetString(10),
    };

    /// <summary>作業の行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkItemRow ReadItem(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        ProjectId = r.GetInt32(6),
        GroupId = r.GetInt32(7),
        SortOrder = r.GetInt32(8),
        Name = r.GetString(9),
        Note = r.GetString(10),
        PriorityCodeNo = r.GetInt32(11),
        StartDate = SqliteValues.ParseDate(r.GetString(12)),
        DueDate = SqliteValues.ParseDate(r.GetString(13)),
        StatusCodeNo = r.GetInt32(14),
        ProgressCodeNo = r.GetInt32(15),
        PlannedHours = SqliteValues.ToHours(r.GetDouble(16)),
        CompletedOn = SqliteValues.ParseDate(r.GetString(17)),
        ActualTotalHours = SqliteValues.ToHours(r.GetDouble(18)),
    };

    /// <summary>日ごとの記録の行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkRecordRow ReadRecord(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        ProjectId = r.GetInt32(6),
        WorkItemId = r.GetInt32(7),
        Date = SqliteValues.ParseDate(r.GetString(8)),
        Hours = SqliteValues.ToHours(r.GetDouble(9)),
        Note = r.GetString(10),
    };

    /// <summary>選びの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkProjectSelectionRow ReadSelection(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        UserId = r.GetInt32(6),
        ProjectId = r.GetInt32(7),
    };

    /// <summary>コードマスタの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static CodeMasterRow ReadCode(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        FeatureNo = r.GetInt32(6),
        CategoryNo = r.GetInt32(7),
        CodeNo = r.GetInt32(8),
        SortOrder = r.GetInt32(9),
        NumericValue = r.GetInt32(10),
        Name = r.GetString(11),
    };

    /// <summary>検索語の中の、<c>LIKE</c> の特別な文字を、そのままの文字として扱うようにする</summary>
    /// <param name="query">検索語</param>
    /// <returns><c>\</c> で逃がした検索語</returns>
    private static string EscapeLike(string query)
        => query.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary><c>IN</c> に並べるパラメーターの名前の並び</summary>
    /// <param name="count">番号の数</param>
    /// <returns><c>@id0,@id1,…</c></returns>
    private static string InList(int count) => string.Join(',', Enumerable.Range(0, count).Select(i => "@id" + i));

    /// <summary><c>IN</c> に並べる番号のパラメーター</summary>
    /// <param name="ids">番号</param>
    /// <returns>パラメーター</returns>
    private static (string, object)[] InParameters(IEnumerable<int> ids) => [.. ids.Select((id, i) => ($"@id{i}", (object)id))];

    /// <summary>検索の結果を、すべて読む</summary>
    /// <typeparam name="T">1 行を変換した型</typeparam>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="map">1 行を変換する処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変換した行の一覧</returns>
    private static async Task<IReadOnlyList<T>> QueryAsync<T>(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        (string, object)[] parameters,
        Func<SqliteDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var command = SqliteValues.Command(connection, transaction, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(map(reader));
        }
        return list;
    }

    /// <summary>複数の <c>SELECT</c> を 1 つのコマンドで送り、結果を順に読む (1 回の往復で受け取る)</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="queries"><c>SELECT</c> と、その 1 行を受け取る処理の並び (結果は、この順に届く)</param>
    /// <param name="parameters">パラメーター (すべての <c>SELECT</c> で共有する)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    private static async Task QueryBatchAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        (string Sql, Action<SqliteDataReader> Map)[] queries,
        (string, object)[] parameters,
        CancellationToken cancellationToken)
    {
        await using var command = SqliteValues.Command(connection, transaction, string.Join(";\n", queries.Select(q => q.Sql)), parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        for (var i = 0; i < queries.Length; i++)
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                queries[i].Map(reader);
            }
            if (i < queries.Length - 1 && !await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("まとめて送った問い合わせの結果が、足りません。");
            }
        }
    }

    /// <summary>1 つの値を返す SQL を実行する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>1 行 1 列目の値。行が無ければ null</returns>
    private static async Task<object?> ScalarAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = SqliteValues.Command(connection, transaction, sql, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    /// <summary>値を返さない SQL を実行する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実行の完了を表すタスク</returns>
    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = SqliteValues.Command(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>更新で変えた列を集める</summary>
    private sealed class ColumnChanges
    {
        /// <summary>代入の並び</summary>
        private readonly List<string> _sets = [];

        /// <summary>パラメーター</summary>
        private readonly List<(string, object)> _parameters = [];

        /// <summary>変えた列が無いか</summary>
        public bool IsEmpty => _sets.Count == 0;

        /// <summary>SET 句の中身</summary>
        public string SetClause => string.Join(", ", _sets);

        /// <summary>パラメーター</summary>
        public IEnumerable<(string, object)> Parameters => _parameters;

        /// <summary>値が違うときだけ、列を足す</summary>
        /// <param name="column">列の名前</param>
        /// <param name="before">更新前の値</param>
        /// <param name="after">更新後の値</param>
        public void Add(string column, object before, object after)
        {
            if (Equals(before, after))
            {
                return;
            }
            _sets.Add($"{column} = @{column}");
            _parameters.Add(($"@{column}", after));
        }
    }
}
