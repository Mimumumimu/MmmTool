namespace MmmTool.WorkItems.Core;

/// <summary>スケジュールの、作業ごとの日の備考を作る</summary>
/// <remarks>
/// 日の備考 (<see cref="WorkRecord.Note"/>)は作業の行にだけあるので、案件・グループの行には作らない。
/// 時間が 0 の日の備考も含める。実績の棒には出ないが、備考は読めるようにするため。
/// </remarks>
public static class ScheduleDayNotes
{
    /// <summary>作業ごとの、日の備考を作る</summary>
    /// <param name="roots">案件の行の並び (絞り込む前)</param>
    /// <param name="records">記録 (木に無い作業の記録と、備考が空の記録は数えない)</param>
    /// <returns>作業の識別ごとの、日付と備考の組。備考が無い作業は含まれない</returns>
    public static IReadOnlyDictionary<WorkKey, IReadOnlyDictionary<DateOnly, string>> Build(IReadOnlyList<WorkNode> roots, IEnumerable<WorkRecord> records)
    {
        var works = roots.SelectMany(r => r.SelfAndDescendants()).Where(n => n.Key.Kind == WorkRowKind.Work).Select(n => n.Key).ToDictionary(key => key.Id);

        var perWork = new Dictionary<WorkKey, Dictionary<DateOnly, string>>();
        foreach (var record in records)
        {
            if (record.IsDeleted || string.IsNullOrWhiteSpace(record.Note) || !works.TryGetValue(record.WorkItemId, out var key))
            {
                continue;
            }

            if (!perWork.TryGetValue(key, out var days))
            {
                perWork[key] = days = [];
            }
            days[record.Date] = record.Note;
        }

        return perWork.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<DateOnly, string>)pair.Value);
    }
}
