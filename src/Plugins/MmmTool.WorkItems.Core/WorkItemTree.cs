namespace MmmTool.WorkItems.Core;

/// <summary>行の一覧から、木構造と集計値を作る</summary>
/// <remarks>
/// 兄弟は、並び (<see cref="WorkItem.SortOrder"/>)、同じなら番号の順。親が見つからない行 (手で直したファイルなど)は、最上位に置く。
/// グループの集計は、下にある作業すべて (段数を問わない)から計算する (規則は docs/specs/work-items.md)。
/// </remarks>
public static class WorkItemTree
{
    /// <summary>木構造を作り、集計値を計算する</summary>
    /// <param name="items">行の一覧 (論理削除済みは含めない)</param>
    /// <param name="records">日ごとの記録 (すべての日)</param>
    /// <param name="date">入力日 (実績 (日)の計算に使う)</param>
    /// <returns>最上位の行の並び</returns>
    public static IReadOnlyList<WorkItemNode> Build(IReadOnlyList<WorkItem> items, IReadOnlyList<WorkRecord> records, DateOnly date)
    {
        var nodes = items.ToDictionary(item => item.Id, item => new WorkItemNode(item));
        var roots = new List<WorkItemNode>();
        foreach (var node in nodes.Values.OrderBy(n => n.Item.SortOrder).ThenBy(n => n.Item.Id))
        {
            if (node.Item.ParentId != 0 && node.Item.ParentId != node.Item.Id && nodes.TryGetValue(node.Item.ParentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        var totals = records.GroupBy(r => r.WorkItemId).ToDictionary(g => g.Key, g => g.Sum(r => r.Hours));
        var days = records.Where(r => r.Date == date).GroupBy(r => r.WorkItemId).ToDictionary(g => g.Key, g => g.Sum(r => r.Hours));
        foreach (var root in roots)
        {
            Summarize(root, totals, days, []);
        }
        return roots;
    }

    /// <summary>行の集計値を、下から順に計算する</summary>
    /// <param name="node">計算する行</param>
    /// <param name="totals">作業ごとの実績 (累計)</param>
    /// <param name="days">作業ごとの入力日の実績</param>
    /// <param name="path">ここまでにたどった行の番号 (手で直したファイルの循環を止める)</param>
    private static void Summarize(WorkItemNode node, Dictionary<int, double> totals, Dictionary<int, double> days, HashSet<int> path)
    {
        var item = node.Item;
        if (!path.Add(item.Id))
        {
            return;
        }

        if (item.Kind == WorkItemKind.Work)
        {
            node.Summary = new WorkItemSummary(
                item.StartDate,
                item.DueDate,
                item.PlannedHours,
                totals.GetValueOrDefault(item.Id),
                days.GetValueOrDefault(item.Id),
                item.Progress);
        }
        else
        {
            foreach (var child in node.Children)
            {
                Summarize(child, totals, days, path);
            }
            node.Summary = SummarizeGroup(node);
        }

        path.Remove(item.Id);
    }

    /// <summary>グループの集計値を、下の作業から計算する</summary>
    /// <param name="group">グループ (子の集計は済んでいること)</param>
    /// <returns>グループの集計値</returns>
    private static WorkItemSummary SummarizeGroup(WorkItemNode group)
    {
        var works = group.Children.SelectMany(c => c.SelfAndDescendants()).Where(n => n.Item.Kind == WorkItemKind.Work).ToList();
        var starts = works.Where(w => w.Item.StartDate is not null).Select(w => w.Item.StartDate!.Value).ToList();
        var dues = works.Where(w => w.Item.DueDate is not null).Select(w => w.Item.DueDate!.Value).ToList();
        var planned = works.Where(w => w.Item.PlannedHours is not null).Select(w => w.Item.PlannedHours!.Value).ToList();

        int? progress = null;
        if (works.Count > 0)
        {
            // すべての作業に予定時間 (0 より大きい)があるときだけ、予定時間で重みを付ける
            var allPlanned = works.All(w => w.Item.PlannedHours is > 0);
            progress = allPlanned
                ? (int)Math.Round(works.Sum(w => w.Item.Progress * w.Item.PlannedHours!.Value) / works.Sum(w => w.Item.PlannedHours!.Value), MidpointRounding.AwayFromZero)
                : (int)Math.Round(works.Average(w => (double)w.Item.Progress), MidpointRounding.AwayFromZero);
        }

        return new WorkItemSummary(
            starts.Count > 0 ? starts.Min() : null,
            dues.Count > 0 ? dues.Max() : null,
            planned.Count > 0 ? planned.Sum() : null,
            works.Sum(w => w.Summary.ActualTotal),
            works.Sum(w => w.Summary.ActualDay),
            progress);
    }
}
