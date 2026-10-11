using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.CodeMaster;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.WorkItems;
using MmmTool.Users.Core;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.SqlServer.WorkItems;

/// <summary>
/// 作業管理を、SQL Server (DB モード)に保存する。
/// </summary>
/// <param name="database">SQL Server に接続して処理を実行する入口</param>
/// <param name="currentUser">今のユーザー (記録の持ち主・作成者・更新者)</param>
/// <remarks>
/// ローカルの SQLite の保存先 (<c>SqliteWorkItemRepository</c>)と同じ設計の表と、同じ読み書きの形 (docs/specs/work-items.md)。
/// 読みは、表示しない案件 (この PC の設定)を除いて、表ごとの別の <c>SELECT</c> で行う。書きは 1 件単位で、更新は変えた列だけ書く。更新日時は、更新の SQL の中で DB サーバーの時計を使って書く。
/// コードマスタ (状態・進捗度・優先度の選択肢)は全員に影響するので、画面から編集しない (表を SQL で直す)。SQL はパラメーター化だけで書く。完全削除 (行を消す)はしない。
/// </remarks>
public sealed class SqlServerWorkItemRepository(SqlServerDatabase database, CurrentUser currentUser) : IWorkItemRepository
{
    /// <summary>「ない」日付 (<c>9999-12-31</c>)</summary>
    private static readonly DateTime NoDate = new(9999, 12, 31);

    /// <summary>一度に <c>IN</c> に並べる番号の最大の数</summary>
    private const int ChunkSize = 500;

    /// <summary>オブジェクト名が正しくない (表が無い)ときの SQL Server のエラー番号</summary>
    private const int InvalidObjectName = 208;

    /// <inheritdoc />
    /// <remarks>ユーザーを特定していないときは 0 (読み込みは、何も返さない。書き込みは <see cref="DataFileException"/>)。</remarks>
    public int UserId => currentUser.User?.Id ?? 0;

    /// <inheritdoc />
    public string? RecoveryMessage => null;

    /// <inheritdoc />
    public bool HasAssignees => true;

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkAssignee>> GetAssigneesAsync(CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<WorkAssignee>>(
            connection => QueryAsync(
                connection,
                null,
                """
                SELECT Id, DisplayName,
                       CASE WHEN IsDeleted = 0 AND ValidFrom <= CAST(SYSDATETIME() AS date) AND CAST(SYSDATETIME() AS date) <= ValidTo THEN 1 ELSE 0 END
                FROM dbo.AppUser
                ORDER BY DisplayName, Id
                """,
                [],
                reader => new WorkAssignee(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2) != 0),
                cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public Task<WorkLoadResult> LoadVisibleAsync(IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default)
        => RunAsync(connection => ReadContentAsync(connection, hiddenProjectIds, exclude: true, withMaster: true, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<WorkLoadResult> LoadProjectsAsync(IReadOnlyList<int> projectIds, CancellationToken cancellationToken = default)
        => projectIds.Count == 0
            ? Task.FromResult(new WorkLoadResult([], [], [], []))
            : RunAsync(connection => ReadContentAsync(connection, projectIds, exclude: false, withMaster: false, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkRecord>> LoadDayAsync(DateOnly date, IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<WorkRecord>>(
            connection => QueryAsync(
                connection,
                null,
                $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM dbo.WorkRecord r WHERE r.Date = @date AND r.IsDeleted = 0 AND {ProjectFilter("r", "ProjectId", exclude: true)}",
                [("@projects", ProjectsJson(hiddenProjectIds)), ("@date", date)],
                reader => ReadRecord(reader).ToWorkRecord(),
                cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public Task<WorkChanges> GetChangesAsync(DateTimeOffset since, DateOnly date, IReadOnlyCollection<int> hiddenProjectIds, CancellationToken cancellationToken = default)
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
                    ($"SELECT {Header("p")}, p.Name, p.Note FROM dbo.WorkProject p WHERE p.UpdatedAt >= @since AND {ProjectFilter("p", "Id", exclude: true)}", r => projects.Add(ReadProject(r).ToWorkProject())),
                    ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Note FROM dbo.WorkGroup g WHERE g.UpdatedAt >= @since AND {ProjectFilter("g", "ProjectId", exclude: true)}", r => groups.Add(ReadGroup(r).ToWorkGroup())),
                    ($"SELECT {Header("i")}, {ItemColumns("i")} FROM dbo.WorkItem i WHERE i.UpdatedAt >= @since AND {ProjectFilter("i", "ProjectId", exclude: true)}", r => items.Add(ReadItem(r).ToWorkItem())),
                    ($"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM dbo.WorkRecord r WHERE r.UpdatedAt >= @since AND r.Date = @date AND {ProjectFilter("r", "ProjectId", exclude: true)}", r => records.Add(ReadRecord(r).ToWorkRecord())),
                    ($"SELECT {Header("c")}, {CodeColumns("c")} FROM dbo.CodeMaster c WHERE c.FeatureNo = @feature AND c.UpdatedAt >= @since", r => codes.Add(ReadCode(r).ToEntry())),
                ],
                parameters,
                cancellationToken).ConfigureAwait(false);
            return new WorkChanges(projects, groups, items, records, codes);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, IReadOnlyCollection<int> hiddenProjectIds, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<WorkProjectEntry>>(
            connection =>
            {
                // 完了済み: 作業が 1 件以上あり、未完了の作業 (状態が完了でない、または完了日が無い)が 1 件も無い。選んでいるか: 選んだ案件の番号 (JSON)に含まれるか
                var sql = $"""
                    SELECT p.Id, p.Name,
                           CASE WHEN {ProjectFilter("p", "Id", exclude: true)} THEN 1 ELSE 0 END,
                           f.IsFinished,
                           CASE WHEN f.IsFinished = 1 THEN (SELECT MAX(w.CompletedDate) FROM dbo.WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0) ELSE @none END
                    FROM dbo.WorkProject p
                    CROSS APPLY (SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0)
                                              AND NOT EXISTS (SELECT 1 FROM dbo.WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND (w.CompletedDate = @none OR w.StatusCodeNo <> 3))
                                         THEN 1 ELSE 0 END AS IsFinished) f
                    WHERE p.IsDeleted = 0
                      AND (@query = N'' OR p.Name LIKE N'%' + @query + N'%' ESCAPE N'\')
                      AND (@activeOnly = 0 OR f.IsFinished = 0)
                    ORDER BY CASE WHEN @selectedFirst = 1 AND {ProjectFilter("p", "Id", exclude: true)} THEN 0 ELSE 1 END, p.Name, p.Id
                    OFFSET @offset ROWS FETCH NEXT @count ROWS ONLY
                    """;
                return QueryAsync(
                    connection,
                    null,
                    sql,
                    [("@projects", ProjectsJson(hiddenProjectIds)), ("@none", NoDate), ("@query", EscapeLike(query)), ("@activeOnly", activeOnly), ("@selectedFirst", selectedFirst), ("@offset", offset), ("@count", count)],
                    reader => new WorkProjectEntry(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetInt32(2) != 0,
                        reader.GetInt32(3) != 0,
                        reader.GetInt32(3) != 0 ? WorkRowValues.ToDate(DateOnly.FromDateTime(reader.GetDateTime(4))) : null),
                    cancellationToken);
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<WorkProject> AddProjectAsync(string name, CancellationToken cancellationToken = default)
    {
        var row = WorkProjectRow.FromWorkProject(new WorkProject { Name = name });
        WorkProject? saved = null;
        await WriteAsync(async (connection, transaction, user) =>
        {
            await using var command = Command(
                connection,
                transaction,
                "INSERT INTO dbo.WorkProject (IsDeleted, CreatedByUserId, UpdatedByUserId, Name, Note) OUTPUT INSERTED.Id, INSERTED.CreatedAt, INSERTED.UpdatedAt VALUES (0, @user, @user, @name, @note)",
                [("@user", user), ("@name", row.Name), ("@note", row.Note)]);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                saved = (row with { Id = reader.GetInt32(0), CreatedAt = reader.GetDateTimeOffset(1), UpdatedAt = reader.GetDateTimeOffset(2), CreatedByUserId = user, UpdatedByUserId = user }).ToWorkProject();
            }
        }, cancellationToken).ConfigureAwait(false);
        return saved!;
    }

    /// <inheritdoc />
    public Task UpdateProjectAsync(int projectId, string? name, string? note, CancellationToken cancellationToken = default)
    {
        var changes = new ColumnChanges();
        if (name is not null)
        {
            changes.Set("Name", WorkRowValues.CheckText(name, "案件の名前", WorkRowValues.NameMax, required: true));
        }
        if (note is not null)
        {
            changes.Set("Note", WorkRowValues.CheckText(note, "案件の備考", WorkRowValues.NoteMax));
        }
        return UpdateColumnsAsync("WorkProject", projectId, changes, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteProjectAsync(int projectId, CancellationToken cancellationToken = default)
        => WriteAsync(async (connection, transaction, user) =>
        {
            (string, object)[] parameters = [("@id", projectId), ("@user", user)];
            await ExecuteAsync(connection, transaction, "UPDATE dbo.WorkProject SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id = @id AND IsDeleted = 0", parameters, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<WorkGroup> AddGroupAsync(WorkGroup group, IReadOnlyList<WorkPosition> siblingPositions, CancellationToken cancellationToken = default)
    {
        var row = WorkGroupRow.FromWorkGroup(group);
        WorkGroup? saved = null;
        await WriteAsync(async (connection, transaction, user) =>
        {
            await ApplyPositionsAsync(connection, transaction, user, siblingPositions, cancellationToken).ConfigureAwait(false);
            await using var command = Command(
                connection,
                transaction,
                "INSERT INTO dbo.WorkGroup (IsDeleted, CreatedByUserId, UpdatedByUserId, ProjectId, ParentGroupId, SortOrder, Name, Note) OUTPUT INSERTED.Id, INSERTED.CreatedAt, INSERTED.UpdatedAt VALUES (0, @user, @user, @project, @parent, @order, @name, @note)",
                [("@user", user), ("@project", row.ProjectId), ("@parent", row.ParentGroupId), ("@order", row.SortOrder), ("@name", row.Name), ("@note", row.Note)]);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            saved = (row with { Id = reader.GetInt32(0), CreatedAt = reader.GetDateTimeOffset(1), UpdatedAt = reader.GetDateTimeOffset(2), CreatedByUserId = user, UpdatedByUserId = user }).ToWorkGroup();
        }, cancellationToken).ConfigureAwait(false);
        return saved!;
    }

    /// <inheritdoc />
    public async Task<WorkItem> AddItemAsync(WorkItem item, IReadOnlyList<WorkPosition> siblingPositions, CancellationToken cancellationToken = default)
    {
        var row = WorkItemRow.FromWorkItem(item);
        WorkItem? saved = null;
        await WriteAsync(async (connection, transaction, user) =>
        {
            await ApplyPositionsAsync(connection, transaction, user, siblingPositions, cancellationToken).ConfigureAwait(false);
            await using var command = Command(
                connection,
                transaction,
                """
                INSERT INTO dbo.WorkItem (IsDeleted, CreatedByUserId, UpdatedByUserId, ProjectId, GroupId, SortOrder, Name, AssigneeUserId,
                                          PriorityCodeNo, StartDate, DueDate, CompletedDate, PlannedHours, ActualTotalHours, StatusCodeNo, ProgressCodeNo, Note)
                OUTPUT INSERTED.Id, INSERTED.CreatedAt, INSERTED.UpdatedAt
                VALUES (0, @user, @user, @project, @group, @order, @name, @assignee, @priority, @start, @due, @completed, @planned, 0, @status, @progress, @note)
                """,
                [
                    ("@user", user), ("@project", row.ProjectId), ("@group", row.GroupId), ("@order", row.SortOrder),
                    ("@name", row.Name), ("@note", row.Note), ("@assignee", row.AssigneeUserId), ("@priority", row.PriorityCodeNo), ("@start", row.StartDate), ("@due", row.DueDate),
                    ("@status", row.StatusCodeNo), ("@progress", row.ProgressCodeNo), ("@planned", row.PlannedHours), ("@completed", row.CompletedDate),
                ]);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            saved = (row with { Id = reader.GetInt32(0), CreatedAt = reader.GetDateTimeOffset(1), UpdatedAt = reader.GetDateTimeOffset(2), CreatedByUserId = user, UpdatedByUserId = user, ActualTotalHours = 0 }).ToWorkItem();
        }, cancellationToken).ConfigureAwait(false);
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
        changes.Add("AssigneeUserId", b.AssigneeUserId, a.AssigneeUserId);
        changes.Add("PriorityCodeNo", b.PriorityCodeNo, a.PriorityCodeNo);
        changes.Add("StartDate", b.StartDate, a.StartDate);
        changes.Add("DueDate", b.DueDate, a.DueDate);
        changes.Add("CompletedDate", b.CompletedDate, a.CompletedDate);
        changes.Add("PlannedHours", b.PlannedHours, a.PlannedHours);
        changes.Add("StatusCodeNo", b.StatusCodeNo, a.StatusCodeNo);
        changes.Add("ProgressCodeNo", b.ProgressCodeNo, a.ProgressCodeNo);
        changes.Add("Note", b.Note, a.Note);
        return UpdateColumnsAsync("WorkItem", a.Id, changes, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdatePositionsAsync(IReadOnlyList<WorkPosition> positions, IReadOnlyList<int> touchedProjectIds, CancellationToken cancellationToken = default)
        => WriteAsync(async (connection, transaction, user) =>
        {
            await ApplyPositionsAsync(connection, transaction, user, positions, cancellationToken).ConfigureAwait(false);
            foreach (var projectId in touchedProjectIds)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "UPDATE dbo.WorkProject SET UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id = @id",
                    [("@user", user), ("@id", projectId)],
                    cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(IReadOnlyList<int> groupIds, IReadOnlyList<int> itemIds, CancellationToken cancellationToken = default)
        => WriteAsync(async (connection, transaction, user) =>
        {
            foreach (var (table, ids) in new[] { ("dbo.WorkGroup", groupIds), ("dbo.WorkItem", itemIds) })
            {
                foreach (var chunk in ids.Chunk(ChunkSize))
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        $"UPDATE {table} SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE IsDeleted = 0 AND Id IN ({InList(chunk.Length)})",
                        [("@user", user), .. InParameters(chunk)],
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<WorkRecordResult> SetRecordAsync(WorkRecord record, CancellationToken cancellationToken = default)
    {
        var user = RequireUser();
        var row = WorkRecordRow.FromWorkRecord(record with { UserId = user });
        WorkRecordResult? result = null;
        await WriteAsync(async (connection, transaction, _) =>
        {
            // 作業の行をロックして (UPDLOCK)、案件の番号を読む。同じ作業の記録を同時に書く人がいても、足し直しの結果が食い違わない
            var projectValue = await ScalarAsync(
                connection,
                transaction,
                "SELECT ProjectId FROM dbo.WorkItem WITH (UPDLOCK, ROWLOCK) WHERE Id = @item AND IsDeleted = 0",
                [("@item", row.WorkItemId)],
                cancellationToken).ConfigureAwait(false);
            if (projectValue is null)
            {
                throw new DataFileException("この作業は、すでに削除されています。", new InvalidOperationException($"WorkItem {row.WorkItemId} がありません。"));
            }
            var projectId = Convert.ToInt32(projectValue);

            var existing = (await QueryAsync(
                connection,
                transaction,
                "SELECT Id, IsDeleted FROM dbo.WorkRecord WHERE WorkItemId = @item AND Date = @date AND CreatedByUserId = @user",
                [("@item", row.WorkItemId), ("@date", row.Date), ("@user", user)],
                reader => (Id: reader.GetInt64(0), IsDeleted: reader.GetBoolean(1)),
                cancellationToken).ConfigureAwait(false)).Cast<(long Id, bool IsDeleted)?>().FirstOrDefault();

            if (record.IsEmpty)
            {
                if (existing is { IsDeleted: false } alive)
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        "UPDATE dbo.WorkRecord SET IsDeleted = 1, Hours = 0, Note = N'', UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id = @id",
                        [("@user", user), ("@id", alive.Id)],
                        cancellationToken).ConfigureAwait(false);
                }
            }
            else if (existing is { } found)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "UPDATE dbo.WorkRecord SET IsDeleted = 0, ProjectId = @project, Hours = @hours, Note = @note, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id = @id",
                    [("@project", projectId), ("@hours", row.Hours), ("@note", row.Note), ("@user", user), ("@id", found.Id)],
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "INSERT INTO dbo.WorkRecord (IsDeleted, CreatedByUserId, UpdatedByUserId, ProjectId, WorkItemId, Date, Hours, Note) VALUES (0, @user, @user, @project, @item, @date, @hours, @note)",
                    [("@user", user), ("@project", projectId), ("@item", row.WorkItemId), ("@date", row.Date), ("@hours", row.Hours), ("@note", row.Note)],
                    cancellationToken).ConfigureAwait(false);
            }

            // その作業の記録をすべて足し直して、作業の行に書く (差分は足さない)
            var hoursList = await QueryAsync(
                connection,
                transaction,
                "SELECT Hours FROM dbo.WorkRecord WHERE WorkItemId = @item AND IsDeleted = 0",
                [("@item", row.WorkItemId)],
                reader => (double)reader.GetDecimal(0),
                cancellationToken).ConfigureAwait(false);
            var total = WorkActualTotal.Calculate(hoursList);
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE dbo.WorkItem SET ActualTotalHours = @total, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id = @item",
                [("@total", (decimal)total), ("@user", user), ("@item", row.WorkItemId)],
                cancellationToken).ConfigureAwait(false);

            var saved = (await QueryAsync(
                connection,
                transaction,
                $"SELECT {Header("r")}, r.ProjectId, r.WorkItemId, r.Date, r.Hours, r.Note FROM dbo.WorkRecord r WHERE r.WorkItemId = @item AND r.Date = @date AND r.CreatedByUserId = @user",
                [("@item", row.WorkItemId), ("@date", row.Date), ("@user", user)],
                reader => ReadRecord(reader).ToWorkRecord(),
                cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            result = new WorkRecordResult(saved, total);
        }, cancellationToken).ConfigureAwait(false);
        return result!;
    }

    /// <summary>ユーザーを特定していることを確かめる</summary>
    /// <returns>今のユーザーの番号</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない (ログインが必要)。</exception>
    private int RequireUser()
        => currentUser.User?.Id ?? throw new DataFileException("ユーザーが特定されていないため、作業管理を使えません (ログインが必要です)。", new InvalidOperationException("今のユーザーが特定されていません。"));

    /// <summary>接続を開いて、処理を実行する (作業管理の表が無いときは、作成の手順を伝えるメッセージにする)</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の結果</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・実行中に失敗した (メッセージは画面に出せる)。</exception>
    private Task<T> RunAsync<T>(Func<SqlConnection, Task<T>> work, CancellationToken cancellationToken)
        => database.RunAsync(async connection =>
        {
            try
            {
                return await work(connection).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                throw new DataFileException("作業管理の表がデータベースにありません。管理者に、003_CreateWorkTables.sql・004_CreateCodeMaster.sql・005_SeedWorkItemCodes.sql を実行してもらってください。", ex);
            }
        }, cancellationToken);

    /// <summary>接続を開いて、結果を返さない処理を実行する (作業管理の表が無いときは、作成の手順を伝えるメッセージにする)</summary>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の完了を表すタスク</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・実行中に失敗した (メッセージは画面に出せる)。</exception>
    private Task RunAsync(Func<SqlConnection, Task> work, CancellationToken cancellationToken)
        => RunAsync(async connection =>
        {
            await work(connection).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

    /// <summary>トランザクションの中で書き込む</summary>
    /// <param name="work">開いた接続・トランザクション・今のユーザーの番号で行う書き込み</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない・接続できない・保存できなかった (メッセージは画面に出せる)。</exception>
    private Task WriteAsync(Func<SqlConnection, SqlTransaction, int, Task> work, CancellationToken cancellationToken)
        => RunAsync(async connection =>
        {
            var user = RequireUser();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await work(connection, transaction, user).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>1 つの表の、変えた列だけを更新する</summary>
    /// <param name="table">表の名前 (<c>dbo</c> なし)</param>
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

        return WriteAsync(
            (connection, transaction, user) => ExecuteAsync(
                connection,
                transaction,
                $"UPDATE dbo.{table} SET {changes.SetClause}, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id = @id AND IsDeleted = 0",
                [.. changes.Parameters, ("@user", user), ("@id", id)],
                cancellationToken),
            cancellationToken);
    }

    /// <summary>行の位置 (案件・親・並び)を書く</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション</param>
    /// <param name="user">今のユーザーの番号</param>
    /// <param name="positions">位置</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <remarks>作業の案件が変わるときは、その作業の記録の案件の番号も直す。</remarks>
    private static async Task ApplyPositionsAsync(SqlConnection connection, SqlTransaction transaction, int user, IReadOnlyList<WorkPosition> positions, CancellationToken cancellationToken)
    {
        foreach (var p in positions)
        {
            (string, object)[] parameters = [("@project", p.ProjectId), ("@parent", p.ParentId), ("@order", p.SortOrder), ("@user", user), ("@id", p.Id)];
            if (p.Kind == WorkRowKind.Group)
            {
                await ExecuteAsync(connection, transaction, "UPDATE dbo.WorkGroup SET ProjectId = @project, ParentGroupId = @parent, SortOrder = @order, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id = @id", parameters, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteAsync(connection, transaction, "UPDATE dbo.WorkItem SET ProjectId = @project, GroupId = @parent, SortOrder = @order, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id = @id", parameters, cancellationToken).ConfigureAwait(false);
                await ExecuteAsync(
                    connection,
                    transaction,
                    "UPDATE dbo.WorkRecord SET ProjectId = @project, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE WorkItemId = @id AND ProjectId <> @project",
                    parameters,
                    cancellationToken).ConfigureAwait(false);
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
    private static async Task<WorkLoadResult> ReadContentAsync(SqlConnection connection, IReadOnlyCollection<int> projectIds, bool exclude, bool withMaster, CancellationToken cancellationToken)
    {
        (string, object)[] parameters = [("@projects", ProjectsJson(projectIds)), ("@feature", WorkCodes.FeatureNo)];
        var projects = new List<WorkProject>();
        var groups = new List<WorkGroup>();
        var items = new List<WorkItem>();
        var codes = new List<CodeMasterEntry>();
        var queries = new List<(string Sql, Action<SqlDataReader> Map)>
        {
            ($"SELECT {Header("p")}, p.Name, p.Note FROM dbo.WorkProject p WHERE p.IsDeleted = 0 AND {ProjectFilter("p", "Id", exclude)}", r => projects.Add(ReadProject(r).ToWorkProject())),
            ($"SELECT {Header("g")}, g.ProjectId, g.ParentGroupId, g.SortOrder, g.Name, g.Note FROM dbo.WorkGroup g WHERE g.IsDeleted = 0 AND {ProjectFilter("g", "ProjectId", exclude)}", r => groups.Add(ReadGroup(r).ToWorkGroup())),
            ($"SELECT {Header("i")}, {ItemColumns("i")} FROM dbo.WorkItem i WHERE i.IsDeleted = 0 AND {ProjectFilter("i", "ProjectId", exclude)}", r => items.Add(ReadItem(r).ToWorkItem())),
        };
        if (withMaster)
        {
            queries.Add(($"SELECT {Header("c")}, {CodeColumns("c")} FROM dbo.CodeMaster c WHERE c.FeatureNo = @feature", r => codes.Add(ReadCode(r).ToEntry())));
        }

        await QueryBatchAsync(connection, null, [.. queries], parameters, cancellationToken).ConfigureAwait(false);
        return new WorkLoadResult(projects, groups, items, codes);
    }

    /// <summary>案件の番号の一覧を、1 つのパラメーター (JSON の配列)にする</summary>
    /// <param name="projectIds">案件の番号</param>
    /// <returns>JSON の配列 (例: <c>[1,5,7]</c>)。番号は整数だけなので、文字の組み立てで足りる</returns>
    /// <remarks>番号をパラメーターに 1 つずつ並べると、数に上限 (2100)があり、問い合わせの形も変わる。1 つの JSON なら、何件でも同じ問い合わせになる。</remarks>
    private static string ProjectsJson(IReadOnlyCollection<int> projectIds) => "[" + string.Join(',', projectIds) + "]";

    /// <summary>案件の番号が、パラメーター <c>@projects</c> (JSON の配列)に、含まれる (または含まれない)という条件</summary>
    /// <param name="alias">表の別名</param>
    /// <param name="column">案件の番号の列</param>
    /// <param name="exclude">含まれないものに絞るか (表示しない案件を除くとき true。番号を指定して読むときは false)</param>
    /// <returns>条件の SQL</returns>
    private static string ProjectFilter(string alias, string column, bool exclude)
        => $"{alias}.{column} {(exclude ? "NOT IN" : "IN")} (SELECT ProjectId FROM OPENJSON(@projects) WITH (ProjectId int '$'))";

    /// <summary>共通ヘッダー (番号・論理削除・作成と更新の日時と人)の列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (6 列)</returns>
    private static string Header(string alias)
        => $"{alias}.Id, {alias}.IsDeleted, {alias}.CreatedAt, {alias}.CreatedByUserId, {alias}.UpdatedAt, {alias}.UpdatedByUserId";

    /// <summary>コードマスタの中身の列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (6 列)</returns>
    private static string CodeColumns(string alias)
        => $"{alias}.FeatureNo, {alias}.CategoryNo, {alias}.CodeNo, {alias}.SortOrder, {alias}.NumericValue, {alias}.Name";

    /// <summary>作業の中身の列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>列の並び (14 列)</returns>
    private static string ItemColumns(string alias)
        => $"{alias}.ProjectId, {alias}.GroupId, {alias}.SortOrder, {alias}.Name, {alias}.AssigneeUserId, {alias}.PriorityCodeNo, {alias}.StartDate, {alias}.DueDate, {alias}.CompletedDate, {alias}.PlannedHours, {alias}.ActualTotalHours, {alias}.StatusCodeNo, {alias}.ProgressCodeNo, {alias}.Note";

    /// <summary>案件の行を読む (共通ヘッダー 6 列のあとに、名前・備考)</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkProjectRow ReadProject(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetBoolean(1),
        CreatedAt = r.GetDateTimeOffset(2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = r.GetDateTimeOffset(4),
        UpdatedByUserId = r.GetInt32(5),
        Name = r.GetString(6),
        Note = r.GetString(7),
    };

    /// <summary>グループの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkGroupRow ReadGroup(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetBoolean(1),
        CreatedAt = r.GetDateTimeOffset(2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = r.GetDateTimeOffset(4),
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
    private static WorkItemRow ReadItem(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetBoolean(1),
        CreatedAt = r.GetDateTimeOffset(2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = r.GetDateTimeOffset(4),
        UpdatedByUserId = r.GetInt32(5),
        ProjectId = r.GetInt32(6),
        GroupId = r.GetInt32(7),
        SortOrder = r.GetInt32(8),
        Name = r.GetString(9),
        AssigneeUserId = r.GetInt32(10),
        PriorityCodeNo = r.GetInt32(11),
        StartDate = DateOnly.FromDateTime(r.GetDateTime(12)),
        DueDate = DateOnly.FromDateTime(r.GetDateTime(13)),
        CompletedDate = DateOnly.FromDateTime(r.GetDateTime(14)),
        PlannedHours = r.GetDecimal(15),
        ActualTotalHours = r.GetDecimal(16),
        StatusCodeNo = r.GetInt32(17),
        ProgressCodeNo = r.GetInt32(18),
        Note = r.GetString(19),
    };

    /// <summary>日ごとの記録の行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static WorkRecordRow ReadRecord(SqlDataReader r) => new()
    {
        Id = r.GetInt64(0),
        IsDeleted = r.GetBoolean(1),
        CreatedAt = r.GetDateTimeOffset(2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = r.GetDateTimeOffset(4),
        UpdatedByUserId = r.GetInt32(5),
        ProjectId = r.GetInt32(6),
        WorkItemId = r.GetInt32(7),
        Date = DateOnly.FromDateTime(r.GetDateTime(8)),
        Hours = r.GetDecimal(9),
        Note = r.GetString(10),
    };

    /// <summary>コードマスタの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static CodeMasterRow ReadCode(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetBoolean(1),
        CreatedAt = r.GetDateTimeOffset(2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = r.GetDateTimeOffset(4),
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
        => query.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal);

    /// <summary><c>IN</c> に並べるパラメーターの名前の並び</summary>
    /// <param name="count">番号の数</param>
    /// <returns><c>@id0,@id1,…</c></returns>
    private static string InList(int count) => string.Join(',', Enumerable.Range(0, count).Select(i => "@id" + i));

    /// <summary><c>IN</c> に並べる番号のパラメーター</summary>
    /// <param name="ids">番号</param>
    /// <returns>パラメーター</returns>
    private static (string, object)[] InParameters(IEnumerable<int> ids) => [.. ids.Select((id, i) => ($"@id{i}", (object)id))];

    /// <summary>コマンドを作り、型を決めたパラメーターを付ける</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL (値はパラメーターで渡す)</param>
    /// <param name="parameters">パラメーターの名前 (<c>@</c> つき)と値</param>
    /// <returns>コマンド。呼ぶ側が破棄する</returns>
    private static SqlCommand Command(SqlConnection connection, SqlTransaction? transaction, string sql, (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(value switch
            {
                int number => new SqlParameter(name, SqlDbType.Int) { Value = number },
                long number => new SqlParameter(name, SqlDbType.BigInt) { Value = number },
                byte number => new SqlParameter(name, SqlDbType.TinyInt) { Value = number },
                bool flag => new SqlParameter(name, SqlDbType.Bit) { Value = flag },
                string text => new SqlParameter(name, SqlDbType.NVarChar, 4000) { Value = text },
                decimal number => new SqlParameter(name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = number },
                DateOnly date => new SqlParameter(name, SqlDbType.Date) { Value = date.ToDateTime(TimeOnly.MinValue) },
                DateTime date => new SqlParameter(name, SqlDbType.Date) { Value = date },
                DateTimeOffset dateTime => new SqlParameter(name, SqlDbType.DateTimeOffset) { Scale = 3, Value = dateTime },
                _ => throw new ArgumentException($"パラメーター {name} の型 {value.GetType().Name} を扱えません。", nameof(parameters)),
            });
        }
        return command;
    }

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
        SqlConnection connection,
        SqlTransaction? transaction,
        string sql,
        (string, object)[] parameters,
        Func<SqlDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, sql, parameters);
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
        SqlConnection connection,
        SqlTransaction? transaction,
        (string Sql, Action<SqlDataReader> Map)[] queries,
        (string, object)[] parameters,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, string.Join(";\n", queries.Select(q => q.Sql)), parameters);
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
    private static async Task<object?> ScalarAsync(SqlConnection connection, SqlTransaction? transaction, string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, sql, parameters);
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
    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction? transaction, string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, sql, parameters);
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
            if (!Equals(before, after))
            {
                Set(column, after);
            }
        }

        /// <summary>列を足す</summary>
        /// <param name="column">列の名前</param>
        /// <param name="value">新しい値</param>
        public void Set(string column, object value)
        {
            _sets.Add($"{column} = @{column}");
            _parameters.Add(($"@{column}", value));
        }
    }
}
