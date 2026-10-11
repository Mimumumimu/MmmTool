namespace MmmTool.WorkItems.Core;

/// <summary>案件の中身の読み込み結果 (論理削除されていない行だけ)</summary>
/// <param name="Projects">案件</param>
/// <param name="Groups">案件の中のグループ</param>
/// <param name="Items">案件の中の作業</param>
/// <param name="SelectedProjectIds">自分が選んでいる案件の番号 (案件を番号で指定して読んだときは、空)</param>
/// <param name="ProgressLabels">進捗度のラベル (値ごと。ラベルが空の値も含む)</param>
public sealed record WorkLoadResult(
    IReadOnlyList<WorkProject> Projects,
    IReadOnlyList<WorkGroup> Groups,
    IReadOnlyList<WorkItem> Items,
    IReadOnlyList<int> SelectedProjectIds,
    IReadOnlyList<WorkProgressLabel> ProgressLabels);
