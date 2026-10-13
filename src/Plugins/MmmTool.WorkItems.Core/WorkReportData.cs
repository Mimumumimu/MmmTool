namespace MmmTool.WorkItems.Core;

/// <summary>作業報告のために読んだ内容 (担当が自分の作業と、その記録)</summary>
/// <param name="Content">案件・グループ・作業と、コードマスタ (論理削除されていない行だけ。表示しない案件も含む)</param>
/// <param name="Records">対象の作業の、期間内の記録 (論理削除されていない行だけ。記録を付けた人は問わない)</param>
/// <param name="DayNotes">期間内の日報メモ (自分の分。空のメモも含む。<see cref="WorkReportBuilder"/> が、空を除く)</param>
/// <remarks>案件・グループは、対象の作業が属する案件の分だけを含む。削除された案件・グループの中の作業は、<see cref="WorkReportBuilder"/> が外す。</remarks>
public sealed record WorkReportData(WorkLoadResult Content, IReadOnlyList<WorkRecord> Records, IReadOnlyList<WorkDayNote> DayNotes);
