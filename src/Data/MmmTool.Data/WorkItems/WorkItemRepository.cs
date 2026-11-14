using System.Data.Common;
using MmmSdk.Core.Components.Storage;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.CodeMaster;
using MmmTool.Data.Queries;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>作業管理の Repository の共通の基底</summary>
/// <param name="gateway">接続を開いて処理を実行する入口</param>
/// <remarks>
/// SQLite と SQL Server で同じ設計の表を、同じ読み書きの流れで扱う。
/// 処理の流れは、ここに 1 つだけ書く。
/// 流れとは、どの表を、どの順に、何回の往復で読み書きするかのこと。
/// DB の種類ごとのプロジェクトに残すのは、次の 3 つだけ。
/// 種類ごとに書き方が違う SQL (<see cref="SqlDialect"/> と、このクラスの抽象メンバー)・ユーザーの持ち方・値の型の読み書き。
/// 読みは表示しない案件 (この PC の設定)を除いて、表ごとの別の <c>SELECT</c> で行う。
/// 木はメモリで組み立てる。
/// 書きは 1 件単位で、更新は変えた列だけ書く。
/// SQL はパラメーター化だけで書く。
/// 完全削除 (行を消す)はしない。
/// </remarks>
public abstract class WorkItemRepository(DbGateway gateway) : IWorkItemRepository
{
    /// <summary>接続を開いて処理を実行する入口</summary>
    protected DbGateway Gateway => gateway;

    /// <summary>DB の種類ごとの SQL の書き方</summary>
    protected SqlDialect Dialect => gateway.Dialect;

    /// <inheritdoc />
    public int UserId => gateway.UserId;

    /// <inheritdoc />
    public abstract string? RecoveryMessage { get; }

    /// <inheritdoc />
    public abstract bool HasAssignees { get; }

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<WorkAssignee>> GetAssigneesAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    /// <remarks>
    /// 完了済み・止めている・選んでいるかを 1 回の問い合わせで返す。
    /// 書き方が DB の種類で大きく違うので、種類ごとに書く。
    /// </remarks>
    public abstract Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, bool selectedOnly, IReadOnlyCollection<int> hiddenProjectIds, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default);

    /// <summary>行の位置を書く SQL</summary>
    /// <remarks>
    /// グループ・作業・作業の記録 (案件が変わるとき)の 3 つの <c>UPDATE</c> を 1 回の往復で行う。
    /// パラメーターは <c>@groups</c> と <c>@items</c> で、どちらも <see cref="PositionJson"/> の JSON。
    /// </remarks>
    protected abstract string ApplyPositionsSql { get; }

    /// <summary>案件の更新日時を書く SQL</summary>
    /// <remarks>パラメーターは <c>@projects</c> で、<see cref="IdJson"/> の JSON。</remarks>
    protected abstract string TouchProjectsSql { get; }

    /// <summary>グループと作業を論理削除する SQL</summary>
    /// <remarks>パラメーターは <c>@groups</c> と <c>@items</c> で、どちらも <see cref="IdJson"/> の JSON。</remarks>
    protected abstract string DeleteSql { get; }

    /// <summary>検索語の中の LIKE の特別な文字をそのままの文字として扱うようにする</summary>
    /// <param name="query">検索語</param>
    /// <returns><c>\</c> で逃がした検索語</returns>
    protected static string EscapeLike(string query)
        => query.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal);

    /// <summary>案件の番号がパラメーター @projectsに含まれるという条件</summary>
    /// <param name="alias">表の別名</param>
    /// <param name="column">案件の番号の列</param>
    /// <param name="exclude">
    /// 含まれないものに絞るか。
    /// 表示しない案件を除くとき true で、番号を指定して読むときは false。
    /// </param>
    /// <returns>条件の SQL</returns>
    /// <remarks>または含まれない</remarks>
    protected string ProjectFilter(string alias, string column, bool exclude)
        => $"{alias}.{column} {(exclude ? "NOT IN" : "IN")} ({Dialect.IdSelect("@projects")})";

    /// <inheritdoc />
    public Task<WorkLoadResult> LoadVisibleAsync(IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default)
        => gateway.ReadAsync(session => ReadContentAsync(session, hiddenProjectIds, exclude: true, withMaster: true, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<WorkLoadResult> LoadProjectsAsync(IReadOnlyList<int> projectIds, CancellationToken cancellationToken = default)
        => projectIds.Count == 0
            ? Task.FromResult(new WorkLoadResult([], [], [], []))
            : gateway.ReadAsync(session => ReadContentAsync(session, projectIds, exclude: false, withMaster: false, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkRecord>> LoadDayAsync(DateOnly date, IReadOnlyCollection<int> projectIds, bool exclude, CancellationToken cancellationToken = default)
        => projectIds.Count == 0 && !exclude
            ? Task.FromResult<IReadOnlyList<WorkRecord>>([])
            : gateway.ReadAsync(
                session => session.QueryAsync(
                    $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM {Dialect.Table("WorkRecord")} r WHERE r.Date = @date AND r.IsDeleted = 0 AND {ProjectFilter("r", "ProjectId", exclude)}",
                    [("@projects", IdJson.From(projectIds)), ("@date", date)],
                    reader => ReadRecord(reader).ToWorkRecord(),
                    cancellationToken),
                cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkRecord>> LoadRecordsAsync(IReadOnlyCollection<int> projectIds, bool exclude, CancellationToken cancellationToken = default)
        => projectIds.Count == 0 && !exclude
            ? Task.FromResult<IReadOnlyList<WorkRecord>>([])
            : gateway.ReadAsync(
                session => session.QueryAsync(
                    $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM {Dialect.Table("WorkRecord")} r WHERE r.IsDeleted = 0 AND (r.Hours > 0 OR r.Note <> '') AND {ProjectFilter("r", "ProjectId", exclude)}",
                    [("@projects", IdJson.From(projectIds))],
                    reader => ReadRecord(reader).ToWorkRecord(),
                    cancellationToken),
                cancellationToken);

    /// <inheritdoc />
    public Task<WorkDayNote?> LoadDayNoteAsync(DateOnly date, CancellationToken cancellationToken = default)
        => gateway.ReadAsync<WorkDayNote?>(
            async session => (await session.QueryAsync(
                $"SELECT {Header("n")}, n.UserId, n.Date, n.Note FROM {Dialect.Table("WorkDayNote")} n WHERE n.UserId = @me AND n.Date = @date",
                [("@me", UserId), ("@date", date)],
                reader => ReadDayNote(reader).ToWorkDayNote(),
                cancellationToken).ConfigureAwait(false)).FirstOrDefault(),
            cancellationToken);

    /// <inheritdoc />
    public Task<WorkReportData> LoadReportAsync(DateOnly from, DateOnly to, int? userId = null, CancellationToken cancellationToken = default)
        => gateway.ReadAsync(session => LoadReportCoreAsync(session, from, to, "= @me", ("@me", userId ?? UserId), cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, WorkReportData>> LoadReportsAsync(DateOnly from, DateOnly to, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return new Dictionary<int, WorkReportData>();
        }

        // 人の番号は、JSON の 1 つのパラメーターで渡す。人数が多くても、同じ問い合わせになる。読んだあとに、人ごとに分ける
        var all = await gateway.ReadAsync(
            session => LoadReportCoreAsync(session, from, to, $"IN ({Dialect.IdSelect("@people")})", ("@people", IdJson.From(userIds)), cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return WorkReportSplitter.Split(all, userIds);
    }

    /// <summary>作業報告の中身を担当者の条件を指定して 1 回の往復で読む</summary>
    /// <param name="session">開いた接続</param>
    /// <param name="from">記録を読み始める日</param>
    /// <param name="to">記録を読み終える日で、報告日。</param>
    /// <param name="assignee">
    /// 担当者の列と比べる条件の後半。
    /// <c>= @me</c> か、<c>IN (…)</c>。
    /// </param>
    /// <param name="assigneeParameter">条件が使うパラメーターの名前と値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>指定の担当者の、作業報告の中身。</returns>
    /// <remarks>
    /// 担当が指定の人で、状態が作業予定・作業中・保留か、期間内に記録があるか、期間内に完了した作業。
    /// 表示しない案件も含む。
    /// 担当者で絞るので、記録を付けた人は問わない。
    /// 日報メモは持ち主が指定の人の行の、報告日の分だけ。
    /// 6 つの問い合わせを 1 回の往復で受け取る。
    /// </remarks>
    private async Task<WorkReportData> LoadReportCoreAsync(DbSession session, DateOnly from, DateOnly to, string assignee, (string, object) assigneeParameter, CancellationToken cancellationToken)
    {
        (string, object)[] parameters = [assigneeParameter, ("@from", from), ("@to", to), ("@closed", (int)WorkItemStatus.Closed), ("@exclAll", (byte)WorkReportExclusion.PlanAndActual), ("@feature", WorkCodes.FeatureNo)];
        var projects = new List<WorkProject>();
        var groups = new List<WorkGroup>();
        var items = new List<WorkItem>();
        var records = new List<WorkRecord>();
        var codes = new List<CodeMasterEntry>();
        var dayNotes = new List<WorkDayNote>();
        var d = Dialect;
        await session.QueryBatchAsync(
            [
                ($"SELECT {Header("p")}, p.Name, p.Link, p.Note FROM {d.Table("WorkProject")} p WHERE p.IsDeleted = 0 AND p.Id IN (SELECT w.ProjectId FROM {d.Table("WorkItem")} w WHERE {ReportItemCondition("w", assignee)})", r => projects.Add(ReadProject(r).ToWorkProject())),
                ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Link, g.Note FROM {d.Table("WorkGroup")} g WHERE g.IsDeleted = 0 AND g.ProjectId IN (SELECT w.ProjectId FROM {d.Table("WorkItem")} w WHERE {ReportItemCondition("w", assignee)})", r => groups.Add(ReadGroup(r).ToWorkGroup())),
                ($"SELECT {Header("i")}, {ItemColumns("i")} FROM {d.Table("WorkItem")} i WHERE {ReportItemCondition("i", assignee)}", r => items.Add(ReadItem(r).ToWorkItem())),
                ($"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM {d.Table("WorkRecord")} r WHERE r.IsDeleted = 0 AND r.Date >= @from AND r.Date <= @to AND r.WorkItemId IN (SELECT w.Id FROM {d.Table("WorkItem")} w WHERE w.IsDeleted = 0 AND w.AssigneeUserId {assignee} AND w.ReportExclusion <> @exclAll)", r => records.Add(ReadRecord(r).ToWorkRecord())),
                ($"SELECT {CodeMasterRowReader.Columns("c")} FROM {d.Table("CodeMaster")} c WHERE c.FeatureNo = @feature", r => codes.Add(CodeMasterRowReader.Read(r, d).ToEntry())),
                ($"SELECT {Header("n")}, n.UserId, n.Date, n.Note FROM {d.Table("WorkDayNote")} n WHERE n.UserId {assignee} AND n.Date = @to", r => dayNotes.Add(ReadDayNote(r).ToWorkDayNote())),
            ],
            parameters,
            cancellationToken).ConfigureAwait(false);
        return new WorkReportData(new WorkLoadResult(projects, groups, items, codes), records, dayNotes);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<int>> GetReportPeopleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => gateway.ReadAsync(
            session =>
            {
                var d = session.Dialect;
                return session.QueryAsync(
                    // 担当者で絞らず、状態・完了日・記録の日付・日報メモの日付から引く。予定の枝は外す印が無い作業だけ、実績の枝は実績と予定から外す作業を除く。索引は作成用スクリプトにある。条件ごとに分けて UNION し、それぞれが索引を使えるようにする
                    $"""
                    SELECT i.AssigneeUserId FROM {d.Table("WorkItem")} i WHERE i.IsDeleted = 0 AND i.AssigneeUserId <> 0 AND i.ReportExclusion = 0 AND i.StatusCodeNo IN ({WorkItemStatuses.ReportSqlList})
                    UNION
                    SELECT i.AssigneeUserId FROM {d.Table("WorkItem")} i WHERE i.IsDeleted = 0 AND i.AssigneeUserId <> 0 AND i.ReportExclusion <> @exclAll AND i.StatusCodeNo = @closed AND i.CompletedDate >= @from AND i.CompletedDate <= @to
                    UNION
                    SELECT i.AssigneeUserId FROM {d.Table("WorkItem")} i WHERE i.IsDeleted = 0 AND i.AssigneeUserId <> 0 AND i.ReportExclusion <> @exclAll AND i.Id IN (SELECT r.WorkItemId FROM {d.Table("WorkRecord")} r WHERE r.IsDeleted = 0 AND r.Date >= @from AND r.Date <= @to)
                    UNION
                    SELECT n.UserId FROM {d.Table("WorkDayNote")} n WHERE n.IsDeleted = 0 AND n.Date = @to AND {d.IsNotBlank("n.Note")}
                    """,
                    [("@from", from), ("@to", to), ("@closed", (int)WorkItemStatus.Closed), ("@exclAll", (byte)WorkReportExclusion.PlanAndActual)],
                    reader => reader.GetInt32(0),
                    cancellationToken);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<DateOnly?> GetLatestRecordedDateAsync(DateOnly before, CancellationToken cancellationToken = default)
        => gateway.ReadAsync<DateOnly?>(
            async session =>
            {
                var d = session.Dialect;

                // 工数が入った日と、作業を完了にした日の、新しいほう。実績と予定から外す作業は数えない
                var value = await session.ScalarAsync(
                    $"""
                    SELECT MAX(d) FROM (
                        SELECT MAX(r.Date) AS d FROM {d.Table("WorkRecord")} r WHERE r.IsDeleted = 0 AND r.Hours > 0 AND r.Date < @before AND r.WorkItemId IN (SELECT w.Id FROM {d.Table("WorkItem")} w WHERE w.IsDeleted = 0 AND w.AssigneeUserId = @me AND w.ReportExclusion <> @exclAll)
                        UNION ALL
                        SELECT MAX(w.CompletedDate) FROM {d.Table("WorkItem")} w WHERE w.IsDeleted = 0 AND w.AssigneeUserId = @me AND w.ReportExclusion <> @exclAll AND w.StatusCodeNo = @closed AND w.CompletedDate < @before
                    ) AS x
                    """,
                    [("@before", before), ("@me", UserId), ("@closed", (int)WorkItemStatus.Closed), ("@exclAll", (byte)WorkReportExclusion.PlanAndActual)],
                    cancellationToken).ConfigureAwait(false);
                return value is null ? null : d.ToDate(value);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<double> GetMyDayHoursAsync(DateOnly date, CancellationToken cancellationToken = default)
        => gateway.ReadAsync(
            async session =>
            {
                var d = session.Dialect;
                var value = await session.ScalarAsync(
                    $"SELECT SUM(r.Hours) FROM {d.Table("WorkRecord")} r WHERE r.Date = @date AND r.IsDeleted = 0 AND r.WorkItemId IN (SELECT w.Id FROM {d.Table("WorkItem")} w WHERE w.IsDeleted = 0 AND w.AssigneeUserId = @me)",
                    [("@date", date), ("@me", UserId)],
                    cancellationToken).ConfigureAwait(false);
                return value is null or DBNull ? 0 : WorkActualTotal.Calculate([Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture)]);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkItem>> LoadAutoEntryItemsAsync(CancellationToken cancellationToken = default)
        => gateway.ReadAsync(
            session =>
            {
                var d = session.Dialect;
                return session.QueryAsync(
                    // 担当が自分で、完了も保留もしていなくて、自動入力の時間がある作業。表示しない案件も含む。索引は IX_WorkItem_AssigneeUserId_AutoHours
                    $"SELECT {Header("i")}, {ItemColumns("i")} FROM {d.Table("WorkItem")} i WHERE i.IsDeleted = 0 AND i.AssigneeUserId = @me AND i.AutoHours > 0 AND i.StatusCodeNo NOT IN ({WorkItemStatuses.AutoEntryStoppedSqlList}) AND i.ProjectId IN (SELECT p.Id FROM {d.Table("WorkProject")} p WHERE p.IsDeleted = 0)",
                    [("@me", UserId)],
                    reader => ReadItem(reader).ToWorkItem(),
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>作業報告の対象の作業の条件</summary>
    /// <param name="alias">作業の表の別名</param>
    /// <param name="assignee">
    /// 担当者の列と比べる条件の後半。
    /// <c>= @me</c> か、<c>IN (…)</c>。
    /// </param>
    /// <returns>
    /// 条件の SQL。
    /// パラメーター <c>@closed</c>・<c>@from</c>・<c>@to</c>・<c>@exclAll</c> と、<paramref name="assignee"/> のパラメーターを使う。
    /// </returns>
    /// <remarks>
    /// 担当が指定の人で、次のどちらか。
    /// 予定の枝は外す印が無く、状態が作業予定・作業中・保留。
    /// 実績の枝は実績と予定から外す印が無く、期間内に完了したか、期間内に記録がある。
    /// </remarks>
    private string ReportItemCondition(string alias, string assignee)
        => $"{alias}.IsDeleted = 0 AND {alias}.AssigneeUserId {assignee} AND (({alias}.ReportExclusion = 0 AND {alias}.StatusCodeNo IN ({WorkItemStatuses.ReportSqlList})) OR ({alias}.ReportExclusion <> @exclAll AND (({alias}.StatusCodeNo = @closed AND {alias}.CompletedDate >= @from AND {alias}.CompletedDate <= @to) OR EXISTS (SELECT 1 FROM {Dialect.Table("WorkRecord")} rr WHERE rr.WorkItemId = {alias}.Id AND rr.IsDeleted = 0 AND rr.Date >= @from AND rr.Date <= @to))))";

    /// <inheritdoc />
    public Task<WorkChanges> GetChangesAsync(DateTimeOffset since, DateOnly date, bool allRecordDates, IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default)
        => gateway.ReadAsync(async session =>
        {
            // 更新日時のインデックスは、削除された行も含むので、論理削除も差分で届く。選んだ案件で絞る。5 つの問い合わせを 1 回の往復で受け取る
            (string, object)[] parameters = [("@projects", IdJson.From(hiddenProjectIds)), ("@since", since), ("@date", date), ("@feature", WorkCodes.FeatureNo)];
            var projects = new List<WorkProject>();
            var groups = new List<WorkGroup>();
            var items = new List<WorkItem>();
            var records = new List<WorkRecord>();
            var codes = new List<CodeMasterEntry>();
            var d = session.Dialect;
            await session.QueryBatchAsync(
                [
                    ($"SELECT {Header("p")}, p.Name, p.Link, p.Note FROM {d.Table("WorkProject")} p WHERE p.UpdatedAt >= @since AND {ProjectFilter("p", "Id", exclude: true)}", r => projects.Add(ReadProject(r).ToWorkProject())),
                    ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Link, g.Note FROM {d.Table("WorkGroup")} g WHERE g.UpdatedAt >= @since AND {ProjectFilter("g", "ProjectId", exclude: true)}", r => groups.Add(ReadGroup(r).ToWorkGroup())),
                    ($"SELECT {Header("i")}, {ItemColumns("i")} FROM {d.Table("WorkItem")} i WHERE i.UpdatedAt >= @since AND {ProjectFilter("i", "ProjectId", exclude: true)}", r => items.Add(ReadItem(r).ToWorkItem())),
                    ($"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM {d.Table("WorkRecord")} r WHERE r.UpdatedAt >= @since {(allRecordDates ? "" : "AND r.Date = @date")} AND {ProjectFilter("r", "ProjectId", exclude: true)}", r => records.Add(ReadRecord(r).ToWorkRecord())),
                    ($"SELECT {CodeMasterRowReader.Columns("c")} FROM {d.Table("CodeMaster")} c WHERE c.FeatureNo = @feature AND c.UpdatedAt >= @since", r => codes.Add(CodeMasterRowReader.Read(r, d).ToEntry())),
                ],
                parameters,
                cancellationToken).ConfigureAwait(false);
            return new WorkChanges(projects, groups, items, records, codes);
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<WorkProject> AddProjectAsync(string name, CancellationToken cancellationToken = default)
    {
        var row = WorkProjectRow.FromWorkProject(new WorkProject { Name = name });
        WorkProject? saved = null;
        await gateway.WriteAsync(async session =>
        {
            var created = await InsertAsync(session, "WorkProject", "Name, Note", "@name, @note", [("@name", row.Name), ("@note", row.Note)], cancellationToken).ConfigureAwait(false);
            saved = (row with { Id = created.Id, CreatedAt = created.CreatedAt, UpdatedAt = created.UpdatedAt, CreatedByUserId = session.UserId, UpdatedByUserId = session.UserId }).ToWorkProject();
        }, cancellationToken).ConfigureAwait(false);
        return saved!;
    }

    /// <inheritdoc />
    public Task UpdateProjectAsync(int projectId, string? name, string? link, string? note, CancellationToken cancellationToken = default)
    {
        var changes = new ColumnChanges();
        if (name is not null)
        {
            changes.Set("Name", WorkRowValues.CheckText(name, "案件の名前", WorkRowValues.NameMax, required: true));
        }
        if (link is not null)
        {
            changes.Set("Link", WorkRowValues.CheckText(link, "案件のリンク", WorkRowValues.LinkMax));
        }
        if (note is not null)
        {
            changes.Set("Note", WorkRowValues.CheckText(note, "案件の備考", WorkRowValues.NoteMax));
        }
        return UpdateColumnsAsync("WorkProject", projectId, changes, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteProjectAsync(int projectId, CancellationToken cancellationToken = default)
        => gateway.WriteAsync(
            session => session.ExecuteAsync(
                $"UPDATE {session.Dialect.Table("WorkProject")} SET IsDeleted = 1, UpdatedAt = {session.Dialect.Now}, UpdatedByUserId = @user WHERE Id = @id AND IsDeleted = 0",
                [("@id", projectId)],
                cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public async Task<WorkGroup> AddGroupAsync(WorkGroup group, IReadOnlyList<WorkPosition> siblingPositions, CancellationToken cancellationToken = default)
    {
        var row = WorkGroupRow.FromWorkGroup(group);
        WorkGroup? saved = null;
        await gateway.WriteAsync(async session =>
        {
            await ApplyPositionsAsync(session, siblingPositions, cancellationToken).ConfigureAwait(false);
            var created = await InsertAsync(
                session,
                "WorkGroup",
                "ProjectId, ParentGroupId, SortOrder, Name, Note",
                "@project, @parent, @order, @name, @note",
                [("@project", row.ProjectId), ("@parent", row.ParentGroupId), ("@order", row.SortOrder), ("@name", row.Name), ("@note", row.Note)],
                cancellationToken).ConfigureAwait(false);
            saved = (row with { Id = created.Id, CreatedAt = created.CreatedAt, UpdatedAt = created.UpdatedAt, CreatedByUserId = session.UserId, UpdatedByUserId = session.UserId }).ToWorkGroup();
        }, cancellationToken).ConfigureAwait(false);
        return saved!;
    }

    /// <inheritdoc />
    public async Task<WorkItem> AddItemAsync(WorkItem item, IReadOnlyList<WorkPosition> siblingPositions, CancellationToken cancellationToken = default)
    {
        var row = WorkItemRow.FromWorkItem(item);
        WorkItem? saved = null;
        await gateway.WriteAsync(async session =>
        {
            await ApplyPositionsAsync(session, siblingPositions, cancellationToken).ConfigureAwait(false);
            var created = await InsertAsync(
                session,
                "WorkItem",
                "ProjectId, GroupId, SortOrder, Name, AssigneeUserId, PriorityCodeNo, StartDate, DueDate, CompletedDate, PlannedHours, ActualTotalHours, ReportExclusion, StatusCodeNo, ProgressCodeNo, Note, AutoHours, AutoWeekdays",
                "@project, @group, @order, @name, @assignee, @priority, @start, @due, @completed, @planned, 0, @reportExclusion, @status, @progress, @note, @autoHours, @autoWeekdays",
                [
                    ("@project", row.ProjectId), ("@group", row.GroupId), ("@order", row.SortOrder),
                    ("@name", row.Name), ("@note", row.Note), ("@reportExclusion", row.ReportExclusion), ("@assignee", row.AssigneeUserId), ("@priority", row.PriorityCodeNo), ("@start", row.StartDate), ("@due", row.DueDate),
                    ("@status", row.StatusCodeNo), ("@progress", row.ProgressCodeNo), ("@planned", row.PlannedHours), ("@completed", row.CompletedDate),
                    ("@autoHours", row.AutoHours), ("@autoWeekdays", row.AutoWeekdays),
                ],
                cancellationToken).ConfigureAwait(false);
            saved = (row with { Id = created.Id, CreatedAt = created.CreatedAt, UpdatedAt = created.UpdatedAt, CreatedByUserId = session.UserId, UpdatedByUserId = session.UserId, ActualTotalHours = 0 }).ToWorkItem();
        }, cancellationToken).ConfigureAwait(false);
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
        changes.Add("ReportExclusion", b.ReportExclusion, a.ReportExclusion);
        changes.Add("StatusCodeNo", b.StatusCodeNo, a.StatusCodeNo);
        changes.Add("ProgressCodeNo", b.ProgressCodeNo, a.ProgressCodeNo);
        changes.Add("Note", b.Note, a.Note);
        changes.Add("AutoHours", b.AutoHours, a.AutoHours);
        changes.Add("AutoWeekdays", b.AutoWeekdays, a.AutoWeekdays);
        return UpdateColumnsAsync("WorkItem", a.Id, changes, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdatePositionsAsync(IReadOnlyList<WorkPosition> positions, IReadOnlyList<int> touchedProjectIds, CancellationToken cancellationToken = default)
        => gateway.WriteAsync(async session =>
        {
            await ApplyPositionsAsync(session, positions, cancellationToken).ConfigureAwait(false);
            if (touchedProjectIds.Count > 0)
            {
                // 案件の数だけ流さず、案件の番号を JSON の 1 つのパラメーターで渡して、1 回で更新する
                await session.ExecuteAsync(TouchProjectsSql, [("@projects", IdJson.From(touchedProjectIds))], cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(IReadOnlyList<int> groupIds, IReadOnlyList<int> itemIds, CancellationToken cancellationToken = default)
        => gateway.WriteAsync(async session =>
        {
            if (groupIds.Count == 0 && itemIds.Count == 0)
            {
                return;
            }

            // 番号は、JSON の 1 つのパラメーターで渡す。何件でも、2 つの UPDATE を 1 回の往復で行う
            await session.ExecuteAsync(DeleteSql, [("@groups", IdJson.From(groupIds)), ("@items", IdJson.From(itemIds))], cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<WorkRecordResult> SetRecordAsync(WorkRecord record, CancellationToken cancellationToken = default)
        => (await WriteRecordAsync(record, onlyIfAbsent: false, cancellationToken).ConfigureAwait(false))!;

    /// <inheritdoc />
    public Task<WorkRecordResult?> AddRecordIfAbsentAsync(WorkRecord record, CancellationToken cancellationToken = default)
        => WriteRecordAsync(record, onlyIfAbsent: true, cancellationToken);

    /// <summary>日ごとの記録の書き込み</summary>
    /// <remarks>記録を書いたあとに、その作業の累計を計算し直す。</remarks>
    /// <param name="record">書く記録</param>
    /// <param name="onlyIfAbsent">true なら、同じ作業・日付の行が (論理削除も含めて)あるとき、何も書かない。</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 書いた記録と累計
    /// 何も書かなかったときは null
    /// </returns>
    private async Task<WorkRecordResult?> WriteRecordAsync(WorkRecord record, bool onlyIfAbsent, CancellationToken cancellationToken)
    {
        var row = WorkRecordRow.FromWorkRecord(record);
        WorkRecordResult? result = null;
        await gateway.WriteAsync(async session =>
        {
            var d = session.Dialect;

            // 作業の行を先に確かめる。案件の番号は、作業の行が正。ロックして読むので、同じ作業の記録を同時に書く人がいても、足し直しの結果が食い違わない
            var projectValue = await session.ScalarAsync(
                $"SELECT ProjectId FROM {d.Table("WorkItem")}{d.RowLockHint} WHERE Id = @item AND IsDeleted = 0",
                [("@item", row.WorkItemId)],
                cancellationToken).ConfigureAwait(false);
            if (projectValue is null)
            {
                throw new DataFileException("この作業は、すでに削除されています。", new InvalidOperationException($"WorkItem {row.WorkItemId} がありません。"));
            }
            var projectId = Convert.ToInt32(projectValue, System.Globalization.CultureInfo.InvariantCulture);

            // 書く前の行を、全列読む。書かなかったときは、この行が結果になる
            var existing = (await session.QueryAsync(
                $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM {d.Table("WorkRecord")} r WHERE r.WorkItemId = @item AND r.Date = @date",
                [("@item", row.WorkItemId), ("@date", row.Date)],
                ReadRecord,
                cancellationToken).ConfigureAwait(false)).FirstOrDefault();

            // 行があれば、論理削除された行も含めて、何も書かない (手で 0 にした日を戻さない)
            if (onlyIfAbsent && existing is not null)
            {
                return;
            }

            // 書いた行は、同じ往復の中で受け取る。書いたあとに読み直さない
            const string returning = "Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, ProjectId, WorkItemId, Date, Hours, Note";
            var saved = existing;
            if (record.IsEmpty)
            {
                if (existing is { IsDeleted: false } alive)
                {
                    saved = (await session.QueryAsync(
                        d.UpdateReturning("WorkRecord", $"IsDeleted = 1, Hours = 0, Note = '', UpdatedAt = {d.Now}, UpdatedByUserId = @user", "Id = @id", returning),
                        [("@id", alive.Id)],
                        ReadRecord,
                        cancellationToken).ConfigureAwait(false)).First();
                }
            }
            else if (existing is { } found)
            {
                saved = (await session.QueryAsync(
                    d.UpdateReturning("WorkRecord", $"IsDeleted = 0, ProjectId = @project, Hours = @hours, Note = @note, UpdatedAt = {d.Now}, UpdatedByUserId = @user", "Id = @id", returning),
                    [("@project", projectId), ("@hours", row.Hours), ("@note", row.Note), ("@id", found.Id)],
                    ReadRecord,
                    cancellationToken).ConfigureAwait(false)).First();
            }
            else
            {
                saved = (await session.QueryAsync(
                    d.InsertReturning("WorkRecord", "ProjectId, WorkItemId, Date, Hours, Note", "@project, @item, @date, @hours, @note", returning),
                    [("@project", projectId), ("@item", row.WorkItemId), ("@date", row.Date), ("@hours", row.Hours), ("@note", row.Note)],
                    ReadRecord,
                    cancellationToken).ConfigureAwait(false)).First();
            }

            // その作業の記録をすべて足し直して、作業の行に書く (差分は足さない)
            var hoursList = await session.QueryAsync(
                $"SELECT Hours FROM {d.Table("WorkRecord")} WHERE WorkItemId = @item AND IsDeleted = 0",
                [("@item", row.WorkItemId)],
                reader => (double)d.ReadHours(reader, 0),
                cancellationToken).ConfigureAwait(false);
            var total = WorkActualTotal.Calculate(hoursList);
            await session.ExecuteAsync(
                $"UPDATE {d.Table("WorkItem")} SET ActualTotalHours = @total, UpdatedAt = {d.Now}, UpdatedByUserId = @user WHERE Id = @item",
                [("@total", (decimal)total), ("@item", row.WorkItemId)],
                cancellationToken).ConfigureAwait(false);

            result = new WorkRecordResult(saved?.ToWorkRecord(), total);
        }, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<WorkDayNote> SetDayNoteAsync(DateOnly date, string note, CancellationToken cancellationToken = default)
    {
        var text = WorkDayNoteRow.CheckNote(note);
        WorkDayNote? result = null;
        await gateway.WriteAsync(async session =>
        {
            var d = session.Dialect;

            // 同じ (持ち主, 日付)の行があれば書き直し、無ければ作る。空にしても、行は消さない。同時に同じ日を書く (別の PC)ときに、一意の索引に当たらないよう、行をロックして確かめる
            // 書いた行は、同じ往復の中で受け取る。書いたあとに読み直さない
            const string returning = "Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, UserId, Date, Note";
            result = (await session.QueryAsync(
                d.UpdateReturning("WorkDayNote", $"Note = @note, UpdatedAt = {d.Now}, UpdatedByUserId = @user", "UserId = @user AND Date = @date", returning, serialize: true),
                [("@note", text), ("@date", date)],
                reader => ReadDayNote(reader).ToWorkDayNote(),
                cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            result ??= (await session.QueryAsync(
                d.InsertReturning("WorkDayNote", "UserId, Date, Note", "@user, @date, @note", returning),
                [("@date", date), ("@note", text)],
                reader => ReadDayNote(reader).ToWorkDayNote(),
                cancellationToken).ConfigureAwait(false)).First();
        }, cancellationToken).ConfigureAwait(false);
        return result!;
    }

    /// <summary>行の追加</summary>
    /// <remarks>行を書いて、採番された番号と日時を同じ往復で受け取る。</remarks>
    /// <param name="session">書き込みの接続</param>
    /// <param name="table">表の名前</param>
    /// <param name="columns">共通ヘッダーを除く、書く列の並び。</param>
    /// <param name="values"><paramref name="columns"/> に対応する値の並び</param>
    /// <param name="parameters">値のパラメーター</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>採番された番号と、作成日時・更新日時。</returns>
    private static async Task<(int Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)> InsertAsync(
        DbSession session, string table, string columns, string values, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        var d = session.Dialect;
        var rows = await session.QueryAsync(
            d.InsertReturning(table, columns, values, "Id, CreatedAt, UpdatedAt"),
            parameters,
            reader => (Id: reader.GetInt32(0), CreatedAt: d.ReadDateTime(reader, 1), UpdatedAt: d.ReadDateTime(reader, 2)),
            cancellationToken).ConfigureAwait(false);
        return rows[0];
    }

    /// <summary>1 つの表の変えた列だけを更新する</summary>
    /// <param name="table">表の名前</param>
    /// <param name="id">行の番号</param>
    /// <param name="changes">変えた列</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 更新の完了を表すタスク。
    /// 変えた列が無ければ、何も書かない。
    /// </returns>
    private Task UpdateColumnsAsync(string table, int id, ColumnChanges changes, CancellationToken cancellationToken)
    {
        if (changes.IsEmpty)
        {
            return Task.CompletedTask;
        }

        return gateway.WriteAsync(
            session => session.ExecuteAsync(
                $"UPDATE {session.Dialect.Table(table)} SET {changes.SetClause}, UpdatedAt = {session.Dialect.Now}, UpdatedByUserId = @user WHERE Id = @id AND IsDeleted = 0",
                [.. changes.Parameters, ("@id", id)],
                cancellationToken),
            cancellationToken);
    }

    /// <summary>行の位置を書く</summary>
    /// <param name="session">書き込みの接続</param>
    /// <param name="positions">位置</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <remarks>
    /// 位置の数だけ流さず、位置を JSON の 1 つのパラメーターで渡して、3 つの UPDATE を 1 回の往復で行う。
    /// 作業の案件が変わるときはその作業の記録の案件の番号も直す。
    /// </remarks>
    private Task ApplyPositionsAsync(DbSession session, IReadOnlyList<WorkPosition> positions, CancellationToken cancellationToken)
        => positions.Count == 0
            ? Task.CompletedTask
            : session.ExecuteAsync(
                ApplyPositionsSql,
                [("@groups", PositionJson.From(positions.Where(p => p.Kind == WorkRowKind.Group))), ("@items", PositionJson.From(positions.Where(p => p.Kind != WorkRowKind.Group)))],
                cancellationToken);

    /// <summary>案件の中身と必要ならコードマスタを 1 回の往復で読む</summary>
    /// <param name="session">開いた接続</param>
    /// <param name="projectIds">案件の番号 (JSON の 1 つのパラメーターで渡す)</param>
    /// <param name="exclude">
    /// その番号を除くか。
    /// 表示しない案件を除いて読むとき true で、番号を指定して読むときは false。
    /// </param>
    /// <param name="withMaster">コードマスタも読むか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込んだ内容 (<paramref name="withMaster"/> が false のときはコードマスタは空)</returns>
    private async Task<WorkLoadResult> ReadContentAsync(DbSession session, IReadOnlyCollection<int> projectIds, bool exclude, bool withMaster, CancellationToken cancellationToken)
    {
        (string, object)[] parameters = [("@projects", IdJson.From(projectIds)), ("@feature", WorkCodes.FeatureNo)];
        var projects = new List<WorkProject>();
        var groups = new List<WorkGroup>();
        var items = new List<WorkItem>();
        var codes = new List<CodeMasterEntry>();
        var d = session.Dialect;
        var queries = new List<(string Sql, Action<DbDataReader> Map)>
        {
            ($"SELECT {Header("p")}, p.Name, p.Link, p.Note FROM {d.Table("WorkProject")} p WHERE p.IsDeleted = 0 AND {ProjectFilter("p", "Id", exclude)}", r => projects.Add(ReadProject(r).ToWorkProject())),
            ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Link, g.Note FROM {d.Table("WorkGroup")} g WHERE g.IsDeleted = 0 AND {ProjectFilter("g", "ProjectId", exclude)}", r => groups.Add(ReadGroup(r).ToWorkGroup())),
            ($"SELECT {Header("i")}, {ItemColumns("i")} FROM {d.Table("WorkItem")} i WHERE i.IsDeleted = 0 AND {ProjectFilter("i", "ProjectId", exclude)}", r => items.Add(ReadItem(r).ToWorkItem())),
        };
        if (withMaster)
        {
            queries.Add(($"SELECT {CodeMasterRowReader.Columns("c")} FROM {d.Table("CodeMaster")} c WHERE c.FeatureNo = @feature", r => codes.Add(CodeMasterRowReader.Read(r, d).ToEntry())));
        }

        await session.QueryBatchAsync([.. queries], parameters, cancellationToken).ConfigureAwait(false);
        return new WorkLoadResult(projects, groups, items, codes);
    }

    /// <summary>共通ヘッダーの列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (6 列)</returns>
    protected static string Header(string alias) => SqlDialect.Header(alias);

    /// <summary>作業の中身の列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (18 列)</returns>
    private static string ItemColumns(string alias)
        => $"{alias}.ProjectId, {alias}.GroupId, {alias}.SortOrder, {alias}.Name, {alias}.AssigneeUserId, {alias}.PriorityCodeNo, {alias}.StartDate, {alias}.DueDate, {alias}.CompletedDate, {alias}.PlannedHours, {alias}.ActualTotalHours, {alias}.Link, {alias}.ReportExclusion, {alias}.StatusCodeNo, {alias}.ProgressCodeNo, {alias}.Note, {alias}.AutoHours, {alias}.AutoWeekdays";

    /// <summary>案件の行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private WorkProjectRow ReadProject(DbDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = Dialect.ReadBool(r, 1),
        CreatedAt = Dialect.ReadDateTime(r, 2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = Dialect.ReadDateTime(r, 4),
        UpdatedByUserId = r.GetInt32(5),
        Name = r.GetString(6),
        Link = r.GetString(7),
        Note = r.GetString(8),
    };

    /// <summary>グループの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private WorkGroupRow ReadGroup(DbDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = Dialect.ReadBool(r, 1),
        CreatedAt = Dialect.ReadDateTime(r, 2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = Dialect.ReadDateTime(r, 4),
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
    private WorkItemRow ReadItem(DbDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = Dialect.ReadBool(r, 1),
        CreatedAt = Dialect.ReadDateTime(r, 2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = Dialect.ReadDateTime(r, 4),
        UpdatedByUserId = r.GetInt32(5),
        ProjectId = r.GetInt32(6),
        GroupId = r.GetInt32(7),
        SortOrder = r.GetInt32(8),
        Name = r.GetString(9),
        AssigneeUserId = r.GetInt32(10),
        PriorityCodeNo = r.GetInt32(11),
        StartDate = Dialect.ReadDate(r, 12),
        DueDate = Dialect.ReadDate(r, 13),
        CompletedDate = Dialect.ReadDate(r, 14),
        PlannedHours = Dialect.ReadHours(r, 15),
        ActualTotalHours = Dialect.ReadHours(r, 16),
        Link = r.GetString(17),
        ReportExclusion = Dialect.ReadByte(r, 18),
        StatusCodeNo = r.GetInt32(19),
        ProgressCodeNo = r.GetInt32(20),
        Note = r.GetString(21),
        AutoHours = Dialect.ReadHours(r, 22),
        AutoWeekdays = Dialect.ReadByte(r, 23),
    };

    /// <summary>日ごとの記録の行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private WorkRecordRow ReadRecord(DbDataReader r) => new()
    {
        Id = r.GetInt64(0),
        IsDeleted = Dialect.ReadBool(r, 1),
        CreatedAt = Dialect.ReadDateTime(r, 2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = Dialect.ReadDateTime(r, 4),
        UpdatedByUserId = r.GetInt32(5),
        ProjectId = r.GetInt32(6),
        WorkItemId = r.GetInt32(7),
        Date = Dialect.ReadDate(r, 8),
        Hours = Dialect.ReadHours(r, 9),
        Note = r.GetString(10),
    };

    /// <summary>日報メモの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private WorkDayNoteRow ReadDayNote(DbDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = Dialect.ReadBool(r, 1),
        CreatedAt = Dialect.ReadDateTime(r, 2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = Dialect.ReadDateTime(r, 4),
        UpdatedByUserId = r.GetInt32(5),
        UserId = r.GetInt32(6),
        Date = Dialect.ReadDate(r, 7),
        Note = r.GetString(8),
    };

}
