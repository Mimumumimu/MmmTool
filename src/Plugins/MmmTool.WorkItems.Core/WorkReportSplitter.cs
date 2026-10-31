namespace MmmTool.WorkItems.Core;

/// <summary>
/// 複数の人をまとめて読んだ作業報告の中身を、人ごとに分ける。
/// </summary>
/// <remarks>
/// 保存先が、人の一覧を 1 回の往復で読み、この部品が、メモリで人ごとに分ける。SQL Server と SQLite で同じ分け方にするため、保存先の外に置く。
/// 1 人分は、その人を指定して読んだ結果と同じ中身になる。
/// </remarks>
public static class WorkReportSplitter
{
    /// <summary>まとめて読んだ中身を、人ごとに分ける</summary>
    /// <param name="all">指定した全員分をまとめて読んだ中身</param>
    /// <param name="userIds">人の番号</param>
    /// <returns>人の番号ごとの中身。作業も日報メモも無い人は、空の中身</returns>
    /// <remarks>
    /// 作業は担当者、日報メモは持ち主で分ける。記録は、属する作業の担当者で分ける。
    /// 案件とグループは、その人の作業が属するものだけにする。コードマスタは全員に同じものを付ける。
    /// </remarks>
    public static IReadOnlyDictionary<int, WorkReportData> Split(WorkReportData all, IReadOnlyCollection<int> userIds)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentNullException.ThrowIfNull(userIds);

        var itemsByUser = all.Content.Items.ToLookup(item => item.AssigneeUserId);
        var assigneeByItem = all.Content.Items.ToDictionary(item => item.Id, item => item.AssigneeUserId);
        var recordsByUser = all.Records.ToLookup(record => assigneeByItem.GetValueOrDefault(record.WorkItemId, -1));
        var notesByUser = all.DayNotes.ToLookup(note => note.UserId);

        var result = new Dictionary<int, WorkReportData>();
        foreach (var userId in userIds.Distinct())
        {
            var items = itemsByUser[userId].ToList();
            var projectIds = items.Select(item => item.ProjectId).ToHashSet();
            var content = new WorkLoadResult(
                [.. all.Content.Projects.Where(project => projectIds.Contains(project.Id))],
                [.. all.Content.Groups.Where(group => projectIds.Contains(group.ProjectId))],
                items,
                all.Content.Codes);
            result[userId] = new WorkReportData(content, [.. recordsByUser[userId]], [.. notesByUser[userId]]);
        }
        return result;
    }
}
