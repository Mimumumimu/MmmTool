using MmmTool.Data.Queries;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.WorkItems;
using MmmTool.Users.Core;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.SqlServer.WorkItems;

/// <summary>作業管理を SQL Serverに保存する</summary>
/// <param name="database">SQL Server に接続して処理を実行する入口</param>
/// <param name="currentUser">今のユーザー (記録の持ち主・作成者・更新者)</param>
/// <remarks>
/// 読み書きの流れは <see cref="WorkItemRepository"/> にあり、ローカルの SQLite の保存先と共通。
/// ここに残すのは、SQL Server だけの SQL と担当者の一覧。
/// 更新日時は更新の SQL の中で DB サーバーの時計を使って書く。
/// コードマスタ (状態・進捗度・優先度の選択肢)は全員に影響するので、画面から編集しない。
/// 表を SQL で直す。
/// 完全削除 (行を消す)はしない。
/// 設計は docs/specs/work-items.md にある。
/// </remarks>
public sealed class SqlServerWorkItemRepository(SqlServerDatabase database, CurrentUser currentUser)
    : WorkItemRepository(new SqlServerGateway(database, currentUser, "作業管理"))
{
    /// <inheritdoc />
    public override string? RecoveryMessage => null;

    /// <inheritdoc />
    public override bool HasAssignees => true;

    /// <inheritdoc />
    protected override string ApplyPositionsSql =>
        """
        UPDATE g SET g.ProjectId = j.ProjectId, g.ParentGroupId = j.ParentId, g.SortOrder = j.SortOrder, g.UpdatedAt = SYSDATETIMEOFFSET(), g.UpdatedByUserId = @user
        FROM dbo.WorkGroup AS g
        INNER JOIN OPENJSON(@groups) WITH (Id int '$.id', ProjectId int '$.project', ParentId int '$.parent', SortOrder int '$.order') AS j ON j.Id = g.Id;

        UPDATE w SET w.ProjectId = j.ProjectId, w.GroupId = j.ParentId, w.SortOrder = j.SortOrder, w.UpdatedAt = SYSDATETIMEOFFSET(), w.UpdatedByUserId = @user
        FROM dbo.WorkItem AS w
        INNER JOIN OPENJSON(@items) WITH (Id int '$.id', ProjectId int '$.project', ParentId int '$.parent', SortOrder int '$.order') AS j ON j.Id = w.Id;

        UPDATE r SET r.ProjectId = j.ProjectId, r.UpdatedAt = SYSDATETIMEOFFSET(), r.UpdatedByUserId = @user
        FROM dbo.WorkRecord AS r
        INNER JOIN OPENJSON(@items) WITH (Id int '$.id', ProjectId int '$.project') AS j ON j.Id = r.WorkItemId
        WHERE r.ProjectId <> j.ProjectId;
        """;

    /// <inheritdoc />
    protected override string TouchProjectsSql
        => "UPDATE dbo.WorkProject SET UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user WHERE Id IN (SELECT ProjectId FROM OPENJSON(@projects) WITH (ProjectId int '$'))";

    /// <inheritdoc />
    protected override string DeleteSql =>
        """
        UPDATE dbo.WorkGroup SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user
        WHERE IsDeleted = 0 AND Id IN (SELECT j.Id FROM OPENJSON(@groups) WITH (Id int '$') AS j);

        UPDATE dbo.WorkItem SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @user
        WHERE IsDeleted = 0 AND Id IN (SELECT j.Id FROM OPENJSON(@items) WITH (Id int '$') AS j);
        """;

    /// <inheritdoc />
    public override Task<IReadOnlyList<WorkAssignee>> GetAssigneesAsync(CancellationToken cancellationToken = default)
        => Gateway.ReadAsync(
            session => session.QueryAsync(
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
    public override Task<IReadOnlyList<WorkProjectEntry>> SearchProjectsAsync(string query, bool activeOnly, bool selectedOnly, IReadOnlyCollection<int> hiddenProjectIds, bool selectedFirst, int offset, int count, CancellationToken cancellationToken = default)
        => Gateway.ReadAsync(
            session =>
            {
                // 止めている: 完了でない作業が 1 件以上あり、そのすべてが止めている状態で、規則は WorkItemStatuses.ContainerStatus と同じ。完了済み: 作業が 1 件以上あり、未完了の作業 (状態が完了でない、または完了日が無い)が 1 件も無い。選んでいるか: 選んだ案件の番号 (JSON)に含まれるか
                var sql = $"""
                    SELECT p.Id, p.Name,
                           CASE WHEN {ProjectFilter("p", "Id", exclude: true)} THEN 1 ELSE 0 END,
                           f.IsFinished,
                           CASE WHEN f.IsFinished = 1 THEN (SELECT MAX(w.CompletedDate) FROM dbo.WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0) ELSE @none END,
                           CASE WHEN EXISTS (SELECT 1 FROM dbo.WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND w.StatusCodeNo IN ({WorkItemStatuses.StoppedSqlList}))
                                 AND NOT EXISTS (SELECT 1 FROM dbo.WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND w.StatusCodeNo <> @closed AND w.StatusCodeNo NOT IN ({WorkItemStatuses.StoppedSqlList}))
                                THEN 1 ELSE 0 END
                    FROM dbo.WorkProject p
                    CROSS APPLY (SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0)
                                              AND NOT EXISTS (SELECT 1 FROM dbo.WorkItem w WHERE w.ProjectId = p.Id AND w.IsDeleted = 0 AND (w.CompletedDate = @none OR w.StatusCodeNo <> @closed))
                                         THEN 1 ELSE 0 END AS IsFinished) f
                    WHERE p.IsDeleted = 0
                      AND (@query = N'' OR p.Name LIKE N'%' + @query + N'%' ESCAPE N'\')
                      AND (@activeOnly = 0 OR f.IsFinished = 0)
                      AND (@selectedOnly = 0 OR {ProjectFilter("p", "Id", exclude: true)})
                    ORDER BY CASE WHEN @selectedFirst = 1 AND {ProjectFilter("p", "Id", exclude: true)} THEN 0 ELSE 1 END, p.Name, p.Id
                    OFFSET @offset ROWS FETCH NEXT @count ROWS ONLY
                    """;
                return session.QueryAsync(
                    sql,
                    [("@projects", IdJson.From(hiddenProjectIds)), ("@none", WorkRowValues.NoDate), ("@closed", (int)WorkItemStatus.Closed), ("@query", EscapeLike(query)), ("@activeOnly", activeOnly), ("@selectedOnly", selectedOnly), ("@selectedFirst", selectedFirst), ("@offset", offset), ("@count", count)],
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
