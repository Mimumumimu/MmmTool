using MmmTool.CodeMaster.Core;

namespace MmmTool.WorkItems.Core;

/// <summary>基準の時刻より後に更新された行 (差分の取得の結果。論理削除された行も含む)</summary>
/// <param name="Projects">選んでいる案件の行</param>
/// <param name="Groups">選んでいる案件の中のグループ</param>
/// <param name="Items">選んでいる案件の中の作業</param>
/// <param name="Records">選んでいる案件の、指定した日の記録 (全員分)</param>
/// <param name="Codes">更新されたコードマスタの件 (論理削除された件も含む)</param>
public sealed record WorkChanges(
    IReadOnlyList<WorkProject> Projects,
    IReadOnlyList<WorkGroup> Groups,
    IReadOnlyList<WorkItem> Items,
    IReadOnlyList<WorkRecord> Records,
    IReadOnlyList<CodeMasterEntry> Codes)
{
    /// <summary>届いた行のうち、いちばん新しい更新日時 (次の基準の時刻にする)。1 行も無ければ null</summary>
    public DateTimeOffset? MaxUpdatedAt => new[]
    {
        Projects.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
        Groups.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
        Items.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
        Records.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
        Codes.Select(r => (DateTimeOffset?)r.UpdatedAt).Max(),
    }.Max();
}
