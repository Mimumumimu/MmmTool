using MmmTool.CodeMaster.Core;

namespace MmmTool.WorkItems.Core;

/// <summary>案件・グループ・作業の一覧から、木構造と集計値を作る</summary>
/// <remarks>
/// 案件は名前の順 (同じなら番号の順)。グループと作業は、同じ親の下で混ざって、並び (<c>SortOrder</c>)、同じなら グループ → 作業、番号の順。
/// 親が見つからない行 (手で直したデータなど)は、その案件の直下に置く。案件が読み込まれていない行は、出さない。
/// 案件・グループの集計は、下にある作業すべて (段数を問わない)から計算する。保存はしない (規則は docs/specs/work-items.md)。
/// </remarks>
public static class WorkItemTree
{
    /// <summary>2 つの案件の並びを比べる (名前の順。同じなら番号の順。<see cref="Build"/> の並びと同じ)</summary>
    /// <param name="nameA">1 つ目の案件の名前</param>
    /// <param name="idA">1 つ目の案件の番号</param>
    /// <param name="nameB">2 つ目の案件の名前</param>
    /// <param name="idB">2 つ目の案件の番号</param>
    /// <returns>1 つ目が後ろなら正の値、前なら負の値、同じなら 0</returns>
    public static int CompareProjects(string nameA, int idA, string nameB, int idB)
    {
        var compared = StringComparer.CurrentCulture.Compare(nameA, nameB);
        return compared != 0 ? compared : idA.CompareTo(idB);
    }

    /// <summary>木構造を作り、集計値を計算する</summary>
    /// <param name="projects">案件 (論理削除済みは含めない)</param>
    /// <param name="groups">グループ (論理削除済みは含めない)</param>
    /// <param name="items">作業 (論理削除済みは含めない)</param>
    /// <param name="dayRecords">入力日の記録 (作業ごとに 1 件。人は区別しない)</param>
    /// <param name="date">入力日 (実績 (日)の計算に使う)</param>
    /// <param name="codes">コードブック (進捗度のコード番号を %の値にするために使う)</param>
    /// <returns>案件の行の並び</returns>
    public static IReadOnlyList<WorkNode> Build(
        IEnumerable<WorkProject> projects,
        IEnumerable<WorkGroup> groups,
        IEnumerable<WorkItem> items,
        IEnumerable<WorkRecord> dayRecords,
        DateOnly date,
        CodeBook codes)
    {
        var roots = projects
            .OrderBy(p => p.Name, StringComparer.CurrentCulture)
            .ThenBy(p => p.Id)
            .Select(p => new WorkNode(new WorkKey(WorkRowKind.Project, p.Id)) { Project = p })
            .ToList();
        var projectNodes = roots.ToDictionary(n => n.Project!.Id);
        var groupNodes = groups.ToDictionary(g => g.Id, g => new WorkNode(new WorkKey(WorkRowKind.Group, g.Id)) { Group = g });

        // 親ごとに、グループと作業を集めてから並べる
        var childrenOf = new Dictionary<WorkNode, List<(int SortOrder, int KindOrder, int Id, WorkNode Node)>>();
        void Attach(WorkNode node, int projectId, int parentGroupId, int sortOrder, int kindOrder)
        {
            WorkNode? parent = null;
            if (parentGroupId != 0 && groupNodes.TryGetValue(parentGroupId, out var parentGroup) && parentGroup.Group!.ProjectId == projectId && !ReferenceEquals(parentGroup, node))
            {
                parent = parentGroup;
            }
            else if (projectNodes.TryGetValue(projectId, out var projectNode))
            {
                parent = projectNode;
            }

            if (parent is null)
            {
                return;
            }
            if (!childrenOf.TryGetValue(parent, out var list))
            {
                childrenOf[parent] = list = [];
            }
            list.Add((sortOrder, kindOrder, node.Key.Id, node));
        }

        foreach (var node in groupNodes.Values)
        {
            Attach(node, node.Group!.ProjectId, node.Group.ParentGroupId, node.Group.SortOrder, 0);
        }
        foreach (var item in items)
        {
            Attach(new WorkNode(new WorkKey(WorkRowKind.Work, item.Id)) { Item = item }, item.ProjectId, item.GroupId, item.SortOrder, 1);
        }
        foreach (var (parent, list) in childrenOf)
        {
            parent.Children.AddRange(list.OrderBy(c => c.SortOrder).ThenBy(c => c.KindOrder).ThenBy(c => c.Id).Select(c => c.Node));
        }

        var days = dayRecords
            .Where(r => r.Date == date && !r.IsDeleted)
            .GroupBy(r => r.WorkItemId)
            .ToDictionary(g => g.Key, g => WorkActualTotal.Calculate(g.Select(r => r.Hours)));
        foreach (var root in roots)
        {
            Summarize(root, days, codes);
        }
        return roots;
    }

    /// <summary>条件に合わない作業の行を、木から外す (表示だけ。集計値は、外す前のまま)</summary>
    /// <param name="roots">案件の行の並び (<see cref="Build"/> の結果)</param>
    /// <param name="keep">残す作業の条件</param>
    /// <remarks>集計 (日付・予定・実績・進捗度・状態)は <see cref="Build"/> で、全部の作業から計算済みなので、外しても変わらない。グループ・案件の行は、中の作業が無くなっても残す。</remarks>
    public static void RemoveWorks(IEnumerable<WorkNode> roots, Func<WorkItem, bool> keep)
    {
        foreach (var node in roots)
        {
            node.Children.RemoveAll(child => child.Item is { } item && !keep(item));
            RemoveWorks(node.Children, keep);
        }
    }

    /// <summary>完了して日がたった作業の行 (と、必要ならグループの行)を、木から外す (表示だけ。集計値は、外す前のまま)</summary>
    /// <param name="roots">案件の行の並び (<see cref="Build"/> の結果)</param>
    /// <param name="date">入力日 (完了日がこの日より前のものが対象。入力日に完了した作業は残す)</param>
    /// <param name="removeGroups">すべて完了して日がたったグループの行も外すか</param>
    /// <remarks>
    /// 案件の行は外さない。グループは、下に作業が 1 件も無いグループ (空)があるときは外さない (空のグループが、親と一緒に見えなくなるのを防ぐため。畳む条件と同じ)。
    /// 外したグループの下は、たどらない。
    /// </remarks>
    public static void RemoveFinished(IEnumerable<WorkNode> roots, DateOnly date, bool removeGroups)
    {
        foreach (var node in roots)
        {
            node.Children.RemoveAll(child => child.Key.Kind switch
            {
                WorkRowKind.Work => child.Summary.IsFinishedBefore(date),
                WorkRowKind.Group => removeGroups
                    && child.Summary.IsFinishedBefore(date)
                    && !child.Children.SelectMany(c => c.SelfAndDescendants()).Any(n => n.Key.Kind == WorkRowKind.Group && n.Summary.WorkCount == 0),
                _ => false,
            });
            RemoveFinished(node.Children, date, removeGroups);
        }
    }

    /// <summary>行の集計値を、下から順に計算する</summary>
    /// <param name="node">計算する行</param>
    /// <param name="days">作業ごとの入力日の実績 (自分の分)</param>
    /// <param name="codes">コードブック</param>
    private static void Summarize(WorkNode node, Dictionary<int, double> days, CodeBook codes)
    {
        if (node.Item is { } item)
        {
            node.Summary = new WorkSummary(
                item.StartDate,
                item.DueDate,
                item.PlannedHours,
                item.ActualTotalHours,
                days.GetValueOrDefault(item.Id),
                WorkCodes.ProgressPercent(codes, item.ProgressCodeNo),
                1,
                item.IsFinished,
                item.IsFinished ? item.CompletedDate : null,
                item.Status);
            return;
        }

        foreach (var child in node.Children)
        {
            Summarize(child, days, codes);
        }
        node.Summary = SummarizeContainer(node, codes);
    }

    /// <summary>案件・グループの集計値を、下の作業から計算する</summary>
    /// <param name="container">案件またはグループ (子の集計は済んでいること)</param>
    /// <param name="codes">コードブック</param>
    /// <returns>集計値</returns>
    private static WorkSummary SummarizeContainer(WorkNode container, CodeBook codes)
    {
        var works = container.Children.SelectMany(c => c.SelfAndDescendants()).Where(n => n.Item is not null).Select(n => n.Item!).ToList();
        var starts = works.Where(w => w.StartDate is not null).Select(w => w.StartDate!.Value).ToList();
        var dues = works.Where(w => w.DueDate is not null).Select(w => w.DueDate!.Value).ToList();
        var planned = works.Where(w => w.PlannedHours is not null).Select(w => w.PlannedHours!.Value).ToList();

        int? progress = null;
        if (works.Count > 0)
        {
            // すべての作業に予定時間 (0 より大きい)があるときだけ、予定時間で重みを付ける
            var allPlanned = works.All(w => w.PlannedHours is > 0);
            progress = allPlanned
                ? (int)Math.Round(works.Sum(w => WorkCodes.ProgressPercent(codes, w.ProgressCodeNo) * w.PlannedHours!.Value) / works.Sum(w => w.PlannedHours!.Value), MidpointRounding.AwayFromZero)
                : (int)Math.Round(works.Average(w => (double)WorkCodes.ProgressPercent(codes, w.ProgressCodeNo)), MidpointRounding.AwayFromZero);
        }

        var finished = works.Count > 0 && works.All(w => w.IsFinished);
        var workNodes = container.Children.SelectMany(c => c.SelfAndDescendants()).Where(n => n.Item is not null).ToList();
        return new WorkSummary(
            starts.Count > 0 ? starts.Min() : null,
            dues.Count > 0 ? dues.Max() : null,
            planned.Count > 0 ? WorkActualTotal.Calculate(planned) : null,
            WorkActualTotal.Calculate(works.Select(w => w.ActualTotalHours)),
            WorkActualTotal.Calculate(workNodes.Select(n => n.Summary.ActualDay)),
            progress,
            works.Count,
            finished,
            finished ? works.Max(w => w.CompletedDate) : null,
            works.Count == 0 ? null
                : works.All(w => w.Status == WorkItemStatus.Open) ? WorkItemStatus.Open
                : works.All(w => w.Status == WorkItemStatus.Closed) ? WorkItemStatus.Closed
                : WorkItemStatus.InProgress);
    }
}
