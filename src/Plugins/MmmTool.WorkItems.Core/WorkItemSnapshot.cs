namespace MmmTool.WorkItems.Core;

/// <summary>保存先から読み込んだ、作業リストの全データ (自分の分)</summary>
/// <param name="Items">グループと作業 (論理削除済みは含まない)</param>
/// <param name="Records">日ごとの記録 (すべての日)</param>
/// <param name="ProgressLabels">進捗度のラベル (ラベルが付いた値だけ。値 → ラベル)</param>
public sealed record WorkItemSnapshot(
    IReadOnlyList<WorkItem> Items,
    IReadOnlyList<WorkRecord> Records,
    IReadOnlyDictionary<int, string> ProgressLabels);
