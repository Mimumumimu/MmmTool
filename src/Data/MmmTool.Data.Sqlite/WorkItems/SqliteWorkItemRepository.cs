using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.Data.Queries;
using MmmTool.Data.WorkItems;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.Sqlite.WorkItems;

/// <summary>作業管理をローカルの SQLiteに保存する</summary>
/// <param name="database">データベースファイルを開く入口</param>
/// <param name="time">更新日時を書くための時計 (SQLite にはサーバーの時計が無いので、アプリが書く)。</param>
/// <remarks>
/// 読み書きの流れは <see cref="WorkItemRepository"/> にあり、SQL Server の保存先と共通。
/// ここに残すのは、SQLite だけの SQL と、担当者が自分だけであること。
/// ユーザーは <c>Id = 1</c> の固定値で、<c>AppUser</c> の表は持たない。
/// 設計は docs/specs/work-items.md にある。
/// </remarks>
public sealed class SqliteWorkItemRepository(SqliteDatabase database, TimeProvider time)
    : WorkItemRepository(new SqliteGateway(database, time))
{
    /// <inheritdoc />
    public override string? RecoveryMessage => database.RecoveryMessage;

    /// <inheritdoc />
    public override bool HasAssignees => false;

    /// <inheritdoc />
    protected override string ApplyPositionsSql =>
        """
        UPDATE WorkGroup SET ProjectId = j.project, ParentGroupId = j.parent, SortOrder = j.sortOrder, UpdatedAt = @now, UpdatedByUserId = @user
        FROM (SELECT json_extract(value, '$.id') AS id, json_extract(value, '$.project') AS project, json_extract(value, '$.parent') AS parent, json_extract(value, '$.order') AS sortOrder FROM json_each(@groups)) AS j
        WHERE WorkGroup.Id = j.id;

        UPDATE WorkItem SET ProjectId = j.project, GroupId = j.parent, SortOrder = j.sortOrder, UpdatedAt = @now, UpdatedByUserId = @user
        FROM (SELECT json_extract(value, '$.id') AS id, json_extract(value, '$.project') AS project, json_extract(value, '$.parent') AS parent, json_extract(value, '$.order') AS sortOrder FROM json_each(@items)) AS j
        WHERE WorkItem.Id = j.id;

        UPDATE WorkRecord SET ProjectId = j.project, UpdatedAt = @now, UpdatedByUserId = @user
        FROM (SELECT json_extract(value, '$.id') AS id, json_extract(value, '$.project') AS project FROM json_each(@items)) AS j
        WHERE WorkRecord.WorkItemId = j.id AND WorkRecord.ProjectId <> j.project;
        """;

    /// <inheritdoc />
    protected override string TouchProjectsSql
        => "UPDATE WorkProject SET UpdatedAt = @now, UpdatedByUserId = @user WHERE Id IN (SELECT value FROM json_each(@projects))";

    /// <inheritdoc />
    protected override string DeleteSql =>
        """
        UPDATE WorkGroup SET IsDeleted = 1, UpdatedAt = @now, UpdatedByUserId = @user
        WHERE IsDeleted = 0 AND Id IN (SELECT value FROM json_each(@groups));

        UPDATE WorkItem SET IsDeleted = 1, UpdatedAt = @now, UpdatedByUserId = @user
        WHERE IsDeleted = 0 AND Id IN (SELECT value FROM json_each(@items));
        """;

    /// <inheritdoc />
    public override Task<IReadOnlyList<WorkAssignee>> GetAssigneesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WorkAssignee>>([new WorkAssignee(UserId, "自分", true)]);

    /// <inheritdoc />
    public override Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, bool selectedOnly, IReadOnlyCollection<int> hiddenProjectIds, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default)
        => Gateway.ReadAsync(
            session =>
            {
                // 完了済み: 作業が 1 件以上あり、未完了の作業 (状態が完了でない、または完了日が無い)が 1 件も無い
                const string finished = "(EXISTS (SELECT 1 FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0) AND NOT EXISTS (SELECT 1 FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND (w.CompletedDate = @none OR w.StatusCodeNo <> @closed)))";

                // 止めている: 完了でない作業が 1 件以上あり、そのすべてが止めている状態で、規則は WorkItemStatuses.ContainerStatus と同じ
                var stopped = $"(EXISTS (SELECT 1 FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND w.StatusCodeNo IN ({WorkItemStatuses.StoppedSqlList})) AND NOT EXISTS (SELECT 1 FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND w.StatusCodeNo <> @closed AND w.StatusCodeNo NOT IN ({WorkItemStatuses.StoppedSqlList})))";
                var sql = $"""
                    SELECT p.Id, p.Name,
                           {ProjectFilter("p", "Id", exclude: true)},
                           {finished},
                           CASE WHEN {finished} THEN (SELECT MAX(w.CompletedDate) FROM WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0) ELSE @none END,
                           {stopped}
                    FROM WorkProject p
                    WHERE p.IsDeleted = 0
                      AND (@query = '' OR p.Name LIKE '%' || @query || '%' ESCAPE '\')
                      AND (@activeOnly = 0 OR NOT {finished})
                      AND (@selectedOnly = 0 OR {ProjectFilter("p", "Id", exclude: true)})
                    ORDER BY CASE WHEN @selectedFirst = 1 AND {ProjectFilter("p", "Id", exclude: true)} THEN 0 ELSE 1 END, p.Name, p.Id
                    LIMIT @count OFFSET @offset
                    """;
                return session.QueryAsync(
                    sql,
                    [("@projects", IdJson.From(hiddenProjectIds)), ("@none", WorkRowValues.NoDate), ("@closed", (int)WorkItemStatus.Closed), ("@query", EscapeLike(query)), ("@activeOnly", activeOnly), ("@selectedOnly", selectedOnly), ("@selectedFirst", selectedFirst), ("@count", count), ("@offset", offset)],
                    reader => new WorkProjectEntry(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        SqlDialect.ReadFlag(reader, 2),
                        SqlDialect.ReadFlag(reader, 3),
                        SqlDialect.ReadFlag(reader, 3) ? WorkRowValues.ToDate(session.Dialect.ReadDate(reader, 4)) : null,
                        SqlDialect.ReadFlag(reader, 5)),
                    cancellationToken);
            },
            cancellationToken);
}
