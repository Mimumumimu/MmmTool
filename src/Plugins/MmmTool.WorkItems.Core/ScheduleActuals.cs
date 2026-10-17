namespace MmmTool.WorkItems.Core;

/// <summary>スケジュールの実績の棒を、行ごとに作る</summary>
/// <remarks>
/// 作業の棒は、その作業の日ごとの記録 (実績が 0 より大きい日)。案件・グループの棒は、下の作業の日ごとの時間の合計。
/// 実績のある最初の日から最後の日までを、空いた日 (土日など)も含めて 1 本につなぐ (細かく切れて、ぶつぶつして見えるため)。
/// </remarks>
public static class ScheduleActuals
{
    /// <summary>行ごとの、実績の棒を作る</summary>
    /// <param name="roots">案件の行の並び (絞り込む前。案件・グループの合計は、全員の作業から計算する)</param>
    /// <param name="records">記録 (木に無い作業の記録と、実績が 0 以下の記録は数えない)</param>
    /// <returns>行の識別ごとの、実績の棒 (日付の順)。実績が無い行は含まれない</returns>
    public static IReadOnlyDictionary<WorkKey, IReadOnlyList<ScheduleActualRun>> Build(IReadOnlyList<WorkNode> roots, IEnumerable<WorkRecord> records)
    {
        // 作業ごとに、その作業と、案件までの祖先の識別を求める (記録を、祖先の合計にも足すため)
        var chains = new Dictionary<int, WorkKey[]>();
        var path = new List<WorkKey>();
        foreach (var root in roots)
        {
            Visit(root, path, chains);
        }

        var perRow = new Dictionary<WorkKey, SortedDictionary<DateOnly, decimal>>();
        foreach (var record in records)
        {
            if (record.IsDeleted || record.Hours <= 0 || !chains.TryGetValue(record.WorkItemId, out var chain))
            {
                continue;
            }

            foreach (var key in chain)
            {
                if (!perRow.TryGetValue(key, out var days))
                {
                    perRow[key] = days = [];
                }
                days[record.Date] = days.GetValueOrDefault(record.Date) + (decimal)record.Hours;
            }
        }

        var result = new Dictionary<WorkKey, IReadOnlyList<ScheduleActualRun>>();
        foreach (var (key, days) in perRow)
        {
            result[key] = Runs(days);
        }
        return result;
    }

    /// <summary>木をたどって、作業ごとの、祖先を含む識別の並びを求める</summary>
    /// <param name="node">たどる行</param>
    /// <param name="path">この行までの識別の並び (たどりながら出し入れする)</param>
    /// <param name="chains">求めた並び (作業の番号 → 案件から作業までの識別)</param>
    private static void Visit(WorkNode node, List<WorkKey> path, Dictionary<int, WorkKey[]> chains)
    {
        path.Add(node.Key);
        if (node.Key.Kind == WorkRowKind.Work)
        {
            chains[node.Key.Id] = [.. path];
        }
        foreach (var child in node.Children)
        {
            Visit(child, path, chains);
        }
        path.RemoveAt(path.Count - 1);
    }

    /// <summary>日ごとの時間を、最初の日から最後の日までの 1 本の棒にまとめる</summary>
    /// <param name="days">日付の順の、日ごとの時間 (1 日以上)</param>
    /// <returns>棒の並び (1 本だけ。実績の無い日は時間 0)</returns>
    private static List<ScheduleActualRun> Runs(SortedDictionary<DateOnly, decimal> days)
    {
        var start = days.Keys.First();
        var hours = new List<double>();
        for (var date = start; date <= days.Keys.Last(); date = date.AddDays(1))
        {
            hours.Add((double)Math.Round(days.GetValueOrDefault(date), 2, MidpointRounding.AwayFromZero));
        }
        return [new ScheduleActualRun(start, hours)];
    }
}
