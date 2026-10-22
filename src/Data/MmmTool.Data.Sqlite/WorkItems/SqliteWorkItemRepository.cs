using Microsoft.Data.Sqlite;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.CodeMaster;
using MmmTool.Data.WorkItems;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.Sqlite.WorkItems;

/// <summary>
/// 作業管理を、ローカルの SQLite (<c>Data/MmmTool.db</c>)に保存する。
/// </summary>
/// <param name="database">データベースファイルを開く入口</param>
/// <param name="time">更新日時を書くための時計 (SQLite にはサーバーの時計が無いので、アプリが書く)</param>
/// <remarks>
/// SQL Server の保存先と同じ設計の表と、同じ読み書きの形 (docs/specs/work-items.md)。ユーザーは <c>Id = 1</c> の固定値で、<c>AppUser</c> の表は持たない。
/// 読みは、表示しない案件 (この PC の設定)を除いて、表ごとの別の <c>SELECT</c> で行う (JOIN で親の列が子の行に重ならない)。木はメモリで組み立てる。
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
    public bool HasAssignees => false;

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkAssignee>> GetAssigneesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WorkAssignee>>([new WorkAssignee(LocalUserId, "自分", true)]);

    /// <inheritdoc />
    public Task<WorkLoadResult> LoadVisibleAsync(IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default)
        => RunAsync(connection => ReadContentAsync(connection, hiddenProjectIds, exclude: true, withMaster: true, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<WorkLoadResult> LoadProjectsAsync(IReadOnlyList<int> projectIds, CancellationToken cancellationToken = default)
        => projectIds.Count == 0
            ? Task.FromResult(new WorkLoadResult([], [], [], []))
            : RunAsync(connection => ReadContentAsync(connection, projectIds, exclude: false, withMaster: false, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkRecord>> LoadDayAsync(DateOnly date, IReadOnlyCollection<int> projectIds, bool exclude, CancellationToken cancellationToken = default)
        => projectIds.Count == 0 && !exclude
            ? Task.FromResult<IReadOnlyList<WorkRecord>>([])
            : RunAsync<IReadOnlyList<WorkRecord>>(
                connection => QueryAsync(
                    connection,
                    null,
                    $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM WorkRecord r WHERE r.Date = @date AND r.IsDeleted = 0 AND {ProjectFilter("r", "ProjectId", exclude)}",
                    [("@projects", ProjectsJson(projectIds)), ("@date", date)],
                    reader => ReadRecord(reader).ToWorkRecord(),
                    cancellationToken),
                cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkRecord>> LoadRecordsAsync(IReadOnlyCollection<int> projectIds, bool exclude, CancellationToken cancellationToken = default)
        => projectIds.Count == 0 && !exclude
            ? Task.FromResult<IReadOnlyList<WorkRecord>>([])
            : RunAsync<IReadOnlyList<WorkRecord>>(
                connection => QueryAsync(
                    connection,
                    null,
                    $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM WorkRecord r WHERE r.IsDeleted = 0 AND r.Hours > 0 AND {ProjectFilter("r", "ProjectId", exclude)}",
                    [("@projects", ProjectsJson(projectIds))],
                    reader => ReadRecord(reader).ToWorkRecord(),
                    cancellationToken),
                cancellationToken);

    /// <inheritdoc />
    public Task<WorkDayNote?> LoadDayNoteAsync(DateOnly date, CancellationToken cancellationToken = default)
        => RunAsync<WorkDayNote?>(
            async connection => (await QueryAsync(
                connection,
                null,
                $"SELECT {Header("n")}, n.UserId, n.Date, n.Note FROM WorkDayNote n WHERE n.UserId = @me AND n.Date = @date",
                [("@me", UserId), ("@date", date)],
                reader => ReadDayNote(reader).ToWorkDayNote(),
                cancellationToken)).FirstOrDefault(),
            cancellationToken);

    /// <inheritdoc />
    public Task<WorkReportData> LoadReportAsync(DateOnly from, DateOnly to, int? userId = null, CancellationToken cancellationToken = default)
        => RunAsync(async connection =>
        {
            // 担当が指定の人 (@me)で、状態が作業予定・作業中か、期間内に記録があるか、期間内に完了した作業。表示しない案件も含む。担当者で絞るので、記録を付けた人は問わない。日報メモは、持ち主が指定の人の行の、報告日の分だけ。6 つの問い合わせを 1 回の往復で受け取る
            (string, object)[] parameters = [("@me", userId ?? UserId), ("@from", from), ("@to", to), ("@planned", (int)WorkItemStatus.Planned), ("@inProgress", (int)WorkItemStatus.InProgress), ("@closed", (int)WorkItemStatus.Closed), ("@feature", WorkCodes.FeatureNo)];
            var projects = new List<WorkProject>();
            var groups = new List<WorkGroup>();
            var items = new List<WorkItem>();
            var records = new List<WorkRecord>();
            var codes = new List<CodeMasterEntry>();
            var dayNotes = new List<WorkDayNote>();
            await QueryBatchAsync(
                connection,
                null,
                [
                    ($"SELECT {Header("p")}, p.Name, p.Link, p.Note FROM WorkProject p WHERE p.IsDeleted = 0 AND p.Id IN (SELECT w.ProjectId FROM WorkItem w WHERE {ReportItemCondition("w")})", r => projects.Add(ReadProject(r).ToWorkProject())),
                    ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Link, g.Note FROM WorkGroup g WHERE g.IsDeleted = 0 AND g.ProjectId IN (SELECT w.ProjectId FROM WorkItem w WHERE {ReportItemCondition("w")})", r => groups.Add(ReadGroup(r).ToWorkGroup())),
                    ($"SELECT {Header("i")}, {ItemColumns("i")} FROM WorkItem i WHERE {ReportItemCondition("i")}", r => items.Add(ReadItem(r).ToWorkItem())),
                    ($"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM WorkRecord r WHERE r.IsDeleted = 0 AND r.Date >= @from AND r.Date <= @to AND r.WorkItemId IN (SELECT w.Id FROM WorkItem w WHERE w.IsDeleted = 0 AND w.AssigneeUserId = @me AND w.ReportExcluded = 0)", r => records.Add(ReadRecord(r).ToWorkRecord())),
                    ($"SELECT {Header("c")}, {CodeColumns("c")} FROM CodeMaster c WHERE c.FeatureNo = @feature", r => codes.Add(ReadCode(r).ToEntry())),
                    ($"SELECT {Header("n")}, n.UserId, n.Date, n.Note FROM WorkDayNote n WHERE n.UserId = @me AND n.Date = @to", r => dayNotes.Add(ReadDayNote(r).ToWorkDayNote())),
                ],
                parameters,
                cancellationToken);
            return new WorkReportData(new WorkLoadResult(projects, groups, items, codes), records, dayNotes);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<int>> GetReportPeopleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<int>>(
            connection => QueryAsync(
                connection,
                null,
                // SQL Server と同じ問い合わせ。担当者で絞らず、状態・完了日・記録の日付・日報メモの日付から引く。作業報告に含めない作業は数えない。索引は schema.sql にある
                """
                SELECT i.AssigneeUserId FROM WorkItem i WHERE i.IsDeleted = 0 AND i.AssigneeUserId <> 0 AND i.ReportExcluded = 0 AND i.StatusCodeNo IN (@planned, @inProgress)
                UNION
                SELECT i.AssigneeUserId FROM WorkItem i WHERE i.IsDeleted = 0 AND i.AssigneeUserId <> 0 AND i.ReportExcluded = 0 AND i.StatusCodeNo = @closed AND i.CompletedDate >= @from AND i.CompletedDate <= @to
                UNION
                SELECT i.AssigneeUserId FROM WorkItem i WHERE i.IsDeleted = 0 AND i.AssigneeUserId <> 0 AND i.ReportExcluded = 0 AND i.Id IN (SELECT r.WorkItemId FROM WorkRecord r WHERE r.IsDeleted = 0 AND r.Date >= @from AND r.Date <= @to)
                UNION
                SELECT n.UserId FROM WorkDayNote n WHERE n.IsDeleted = 0 AND n.Date = @to AND TRIM(n.Note) <> ''
                """,
                [("@from", from), ("@to", to), ("@planned", (int)WorkItemStatus.Planned), ("@inProgress", (int)WorkItemStatus.InProgress), ("@closed", (int)WorkItemStatus.Closed)],
                reader => reader.GetInt32(0),
                cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public Task<DateOnly?> GetLatestRecordedDateAsync(DateOnly before, CancellationToken cancellationToken = default)
        => RunAsync<DateOnly?>(
            async connection =>
            {
                // 工数が入った日と、作業を完了にした日の、新しいほう。作業報告に含めない作業は数えない
                var value = await ScalarAsync(
                    connection,
                    null,
                    """
                    SELECT MAX(d) FROM (
                        SELECT MAX(r.Date) AS d FROM WorkRecord r WHERE r.IsDeleted = 0 AND r.Hours > 0 AND r.Date < @before AND r.WorkItemId IN (SELECT w.Id FROM WorkItem w WHERE w.IsDeleted = 0 AND w.AssigneeUserId = @me AND w.ReportExcluded = 0)
                        UNION ALL
                        SELECT MAX(w.CompletedDate) FROM WorkItem w WHERE w.IsDeleted = 0 AND w.AssigneeUserId = @me AND w.ReportExcluded = 0 AND w.StatusCodeNo = @closed AND w.CompletedDate < @before
                    )
                    """,
                    [("@before", before), ("@me", UserId), ("@closed", (int)WorkItemStatus.Closed)],
                    cancellationToken).ConfigureAwait(false);
                return value is null ? null : SqliteValues.ParseDate(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!);
            },
            cancellationToken);

    /// <summary>作業報告の対象の作業の条件 (担当が自分で、作業報告に含めない印が無く、状態が作業予定・作業中か、期間内に記録があるか、期間内に完了した)</summary>
    /// <param name="alias">作業の表の別名</param>
    /// <returns>条件の SQL (パラメーター <c>@me</c>・<c>@planned</c>・<c>@inProgress</c>・<c>@closed</c>・<c>@from</c>・<c>@to</c> を使う)</returns>
    private static string ReportItemCondition(string alias)
        => $"{alias}.IsDeleted = 0 AND {alias}.AssigneeUserId = @me AND {alias}.ReportExcluded = 0 AND ({alias}.StatusCodeNo IN (@planned, @inProgress) OR ({alias}.StatusCodeNo = @closed AND {alias}.CompletedDate >= @from AND {alias}.CompletedDate <= @to) OR EXISTS (SELECT 1 FROM WorkRecord rr WHERE rr.WorkItemId = {alias}.Id AND rr.IsDeleted = 0 AND rr.Date >= @from AND rr.Date <= @to))";

    /// <inheritdoc />
    public Task<WorkChanges> GetChangesAsync(DateTimeOffset since, DateOnly date, bool allRecordDates, IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default)
        => RunAsync(async connection =>
        {
            // 更新日時のインデックスは、削除された行も含むので、論理削除も差分で届く。選んだ案件で絞る。5 つの問い合わせを 1 回の往復で受け取る
            (string, object)[] parameters = [("@projects", ProjectsJson(hiddenProjectIds)), ("@since", since), ("@date", date), ("@feature", WorkCodes.FeatureNo)];
            var projects = new List<WorkProject>();
            var groups = new List<WorkGroup>();
            var items = new List<WorkItem>();
            var records = new List<WorkRecord>();
            var codes = new List<CodeMasterEntry>();
            await QueryBatchAsync(
                connection,
                null,
                [
                    ($"SELECT {Header("p")}, p.Name, p.Link, p.Note FROM WorkProject p WHERE p.UpdatedAt >= @since AND {ProjectFilter("p", "Id", exclude: true)}", r => projects.Add(ReadProject(r).ToWorkProject())),
                    ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Link, g.Note FROM WorkGroup g WHERE g.UpdatedAt >= @since AND {ProjectFilter("g", "ProjectId", exclude: true)}", r => groups.Add(ReadGroup(r).ToWorkGroup())),
                    ($"SELECT {Header("i")}, {ItemColumns("i")} FROM WorkItem i WHERE i.UpdatedAt >= @since AND {ProjectFilter("i", "ProjectId", exclude: true)}", r => items.Add(ReadItem(r).ToWorkItem())),
                    ($"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM WorkRecord r WHERE r.UpdatedAt >= @since {(allRecordDates ? "" : "AND r.Date = @date")} AND {ProjectFilter("r", "ProjectId", exclude: true)}", r => records.Add(ReadRecord(r).ToWorkRecord())),
                    ($"SELECT {Header("c")}, {CodeColumns("c")} FROM CodeMaster c WHERE c.FeatureNo = @feature AND c.UpdatedAt >= @since", r => codes.Add(ReadCode(r).ToEntry())),
                ],
                parameters,
                cancellationToken);
            return new WorkChanges(projects, groups, items, records, codes);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, bool selectedOnly, IReadOnlyCollection<int> hiddenProjectIds, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<WorkProjectEntry>>(
            connection =>
            {
                // 完了済み: 作業が 1 件以上あり、未完了の作業 (状態が完了でない、または完了日が無い)が 1 件も無い
                const string finished = "(EXISTS (SELECT 1 FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0) AND NOT EXISTS (SELECT 1 FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND (w.CompletedDate = @none OR w.StatusCodeNo <> @closed)))";
                var sql = $"""
                    SELECT p.Id, p.Name,
                           {ProjectFilter("p", "Id", exclude: true)},
                           {finished},
                           CASE WHEN {finished} THEN (SELECT MAX(w.CompletedDate) FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0) ELSE @none END
                    FROM WorkProject p
                    WHERE p.IsDeleted = 0
                      AND (@query = '' OR p.Name LIKE '%' || @query || '%' ESCAPE '\')
                      AND (@activeOnly = 0 OR NOT {finished})
                      AND (@selectedOnly = 0 OR {ProjectFilter("p", "Id", exclude: true)})
                    ORDER BY CASE WHEN @selectedFirst = 1 AND {ProjectFilter("p", "Id", exclude: true)} THEN 0 ELSE 1 END, p.Name, p.Id
                    LIMIT @count OFFSET @offset
                    """;
                return QueryAsync(
                    connection,
                    null,
                    sql,
                    [("@projects", ProjectsJson(hiddenProjectIds)), ("@none", NoDateText), ("@closed", (int)WorkItemStatus.Closed), ("@query", EscapeLike(query)), ("@activeOnly", activeOnly), ("@selectedOnly", selectedOnly), ("@selectedFirst", selectedFirst), ("@count", count), ("@offset", offset)],
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
            saved = (row with { Id = id, CreatedAt = now, UpdatedAt = now, CreatedByUserId = LocalUserId, UpdatedByUserId = LocalUserId }).ToWorkProject();
        }, cancellationToken);
        return saved!;
    }

    /// <inheritdoc />
    public Task UpdateProjectAsync(int projectId, string? name, string? link, string? note, CancellationToken cancellationToken = default)
    {
        var sets = new List<string>();
        var parameters = new List<(string, object)>();
        if (name is not null)
        {
            sets.Add("Name = @name");
            parameters.Add(("@name", WorkRowValues.CheckText(name, "案件の名前", WorkRowValues.NameMax, required: true)));
        }
        if (link is not null)
        {
            sets.Add("Link = @link");
            parameters.Add(("@link", WorkRowValues.CheckText(link, "案件のリンク", WorkRowValues.LinkMax)));
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
                INSERT INTO WorkItem (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, AssigneeUserId,
                                      PriorityCodeNo, StartDate, DueDate, CompletedDate, PlannedHours, ActualTotalHours, ReportExcluded, StatusCodeNo, ProgressCodeNo, Note)
                VALUES (0, @now, @user, @now, @user, @project, @group, @order, @name, @assignee, @priority, @start, @due, @completed, @planned, 0, @reportExcluded, @status, @progress, @note)
                """,
                [
                    ("@now", now), ("@user", LocalUserId), ("@project", row.ProjectId), ("@group", row.GroupId), ("@order", row.SortOrder),
                    ("@name", row.Name), ("@reportExcluded", row.ReportExcluded ? 1 : 0), ("@assignee", row.AssigneeUserId), ("@note", row.Note), ("@priority", row.PriorityCodeNo), ("@start", row.StartDate), ("@due", row.DueDate),
                    ("@status", row.StatusCodeNo), ("@progress", row.ProgressCodeNo), ("@planned", row.PlannedHours), ("@completed", row.CompletedDate),
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
        changes.Add("Link", b.Link, a.Link);
        changes.Add("Note", b.Note, a.Note);
        return UpdateColumnsAsync("WorkGroup", a.Id, changes, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateItemAsync(WorkItem before, WorkItem after, CancellationToken cancellationToken = default)
    {
        var (b, a) = (WorkItemRow.FromWorkItem(before), WorkItemRow.FromWorkItem(after));
        var changes = new ColumnChanges();
        changes.Add("Name", b.Name, a.Name);
        changes.Add("AssigneeUserId", b.AssigneeUserId, a.AssigneeUserId);
        changes.Add("PriorityCodeNo", b.PriorityCodeNo, a.PriorityCodeNo);
        changes.Add("StartDate", b.StartDate, a.StartDate);
        changes.Add("DueDate", b.DueDate, a.DueDate);
        changes.Add("CompletedDate", b.CompletedDate, a.CompletedDate);
        changes.Add("PlannedHours", b.PlannedHours, a.PlannedHours);
        changes.Add("Link", b.Link, a.Link);
        changes.Add("ReportExcluded", b.ReportExcluded, a.ReportExcluded);
        changes.Add("StatusCodeNo", b.StatusCodeNo, a.StatusCodeNo);
        changes.Add("ProgressCodeNo", b.ProgressCodeNo, a.ProgressCodeNo);
        changes.Add("Note", b.Note, a.Note);
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
        var row = WorkRecordRow.FromWorkRecord(record);
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
                "SELECT Id, IsDeleted FROM WorkRecord WHERE WorkItemId = @item AND Date = @date",
                [("@item", row.WorkItemId), ("@date", row.Date)],
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
                $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM WorkRecord r WHERE r.WorkItemId = @item AND r.Date = @date",
                [("@item", row.WorkItemId), ("@date", row.Date)],
                reader => ReadRecord(reader).ToWorkRecord(),
                cancellationToken)).FirstOrDefault();
            result = new WorkRecordResult(saved, total);
        }, cancellationToken);
        return result!;
    }

    /// <inheritdoc />
    public async Task<WorkDayNote> SetDayNoteAsync(DateOnly date, string note, CancellationToken cancellationToken = default)
    {
        var text = WorkDayNoteRow.CheckNote(note);
        var now = Now;
        WorkDayNote? result = null;
        await RunInTransactionAsync(async (connection, transaction) =>
        {
            // 同じ (持ち主, 日付)の行があれば書き直し、無ければ作る。空にしても、行は消さない
            var updated = await ExecuteAsync(
                connection,
                transaction,
                "UPDATE WorkDayNote SET Note = @note, UpdatedAt = @now, UpdatedByUserId = @user WHERE UserId = @user AND Date = @date",
                [("@note", text), ("@now", now), ("@user", LocalUserId), ("@date", date)],
                cancellationToken);
            if (updated == 0)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "INSERT INTO WorkDayNote (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, UserId, Date, Note) VALUES (0, @now, @user, @now, @user, @user, @date, @note)",
                    [("@now", now), ("@user", LocalUserId), ("@date", date), ("@note", text)],
                    cancellationToken);
            }

            result = (await QueryAsync(
                connection,
                transaction,
                $"SELECT {Header("n")}, n.UserId, n.Date, n.Note FROM WorkDayNote n WHERE n.UserId = @user AND n.Date = @date",
                [("@user", LocalUserId), ("@date", date)],
                reader => ReadDayNote(reader).ToWorkDayNote(),
                cancellationToken)).First();
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

    /// <summary>案件の中身 (案件の行・グループ・作業)と、必要ならコードマスタを、1 回の往復で読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="projectIds">案件の番号 (JSON の 1 つのパラメーターで渡す)</param>
    /// <param name="exclude">その番号を除くか (表示しない案件を除いて読むとき true。番号を指定して読むときは false)</param>
    /// <param name="withMaster">コードマスタも読むか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込んだ内容 (<paramref name="withMaster"/> が false のときは、コードマスタは空)</returns>
    private static async Task<WorkLoadResult> ReadContentAsync(SqliteConnection connection, IReadOnlyCollection<int> projectIds, bool exclude, bool withMaster, CancellationToken cancellationToken)
    {
        (string, object)[] parameters = [("@projects", ProjectsJson(projectIds)), ("@feature", WorkCodes.FeatureNo)];
        var projects = new List<WorkProject>();
        var groups = new List<WorkGroup>();
        var items = new List<WorkItem>();
        var codes = new List<CodeMasterEntry>();
        var queries = new List<(string Sql, Action<SqliteDataReader> Map)>
        {
            ($"SELECT {Header("p")}, p.Name, p.Link, p.Note FROM WorkProject p WHERE p.IsDeleted = 0 AND {ProjectFilter("p", "Id", exclude)}", r => projects.Add(ReadProject(r).ToWorkProject())),
            ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Link, g.Note FROM WorkGroup g WHERE g.IsDeleted = 0 AND {ProjectFilter("g", "ProjectId", exclude)}", r => groups.Add(ReadGroup(r).ToWorkGroup())),
            ($"SELECT {Header("i")}, {ItemColumns("i")} FROM WorkItem i WHERE i.IsDeleted = 0 AND {ProjectFilter("i", "ProjectId", exclude)}", r => items.Add(ReadItem(r).ToWorkItem())),
        };
        if (withMaster)
        {
            queries.Add(($"SELECT {Header("c")}, {CodeColumns("c")} FROM CodeMaster c WHERE c.FeatureNo = @feature", r => codes.Add(ReadCode(r).ToEntry())));
        }

        await QueryBatchAsync(connection, null, [.. queries], parameters, cancellationToken);
        return new WorkLoadResult(projects, groups, items, codes);
    }

    /// <summary>案件の番号の一覧を、1 つのパラメーター (JSON の配列)にする</summary>
    /// <param name="projectIds">案件の番号</param>
    /// <returns>JSON の配列 (例: <c>[1,5,7]</c>)。番号は整数だけなので、文字の組み立てで足りる</returns>
    /// <remarks>SQL Server の保存先と同じ形 (何件でも同じ問い合わせになる)。</remarks>
    private static string ProjectsJson(IReadOnlyCollection<int> projectIds) => "[" + string.Join(',', projectIds) + "]";

    /// <summary>案件の番号が、パラメーター <c>@projects</c> (JSON の配列)に、含まれる (または含まれない)という条件</summary>
    /// <param name="alias">表の別名</param>
    /// <param name="column">案件の番号の列</param>
    /// <param name="exclude">含まれないものに絞るか (表示しない案件を除くとき true。番号を指定して読むときは false)</param>
    /// <returns>条件の SQL</returns>
    private static string ProjectFilter(string alias, string column, bool exclude)
        => $"{alias}.{column} {(exclude ? "NOT IN" : "IN")} (SELECT value FROM json_each(@projects))";

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
    /// <returns>列の並び (16 列)</returns>
    private static string ItemColumns(string alias)
        => $"{alias}.ProjectId, {alias}.GroupId, {alias}.SortOrder, {alias}.Name, {alias}.AssigneeUserId, {alias}.PriorityCodeNo, {alias}.StartDate, {alias}.DueDate, {alias}.CompletedDate, {alias}.PlannedHours, {alias}.ActualTotalHours, {alias}.Link, {alias}.ReportExcluded, {alias}.StatusCodeNo, {alias}.ProgressCodeNo, {alias}.Note";

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
        Link = r.GetString(7),
        Note = r.GetString(8),
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
        Link = r.GetString(10),
        Note = r.GetString(11),
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
        AssigneeUserId = r.GetInt32(10),
        PriorityCodeNo = r.GetInt32(11),
        StartDate = SqliteValues.ParseDate(r.GetString(12)),
        DueDate = SqliteValues.ParseDate(r.GetString(13)),
        CompletedDate = SqliteValues.ParseDate(r.GetString(14)),
        PlannedHours = SqliteValues.ToHours(r.GetDouble(15)),
        ActualTotalHours = SqliteValues.ToHours(r.GetDouble(16)),
        Link = r.GetString(17),
        ReportExcluded = r.GetInt64(18) != 0,
        StatusCodeNo = r.GetInt32(19),
        ProgressCodeNo = r.GetInt32(20),
        Note = r.GetString(21),
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

    /// <summary>日報メモの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkDayNoteRow ReadDayNote(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        UserId = r.GetInt32(6),
        Date = SqliteValues.ParseDate(r.GetString(7)),
        Note = r.GetString(8),
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
    /// <returns>変わった行の数</returns>
    private static async Task<int> ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = SqliteValues.Command(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
