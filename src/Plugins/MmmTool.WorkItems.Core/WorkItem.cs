namespace MmmTool.WorkItems.Core;

/// <summary>グループまたは作業の 1 行 (保存するデータ。保存先に依存しない)</summary>
/// <remarks>
/// 親は <see cref="ParentId"/> で指す (0 は最上位)。日付・予定時間の「無い」は null (DB では <c>9999-12-31</c>・0 に置き換える)。
/// グループでは、日付・優先度・状態・進捗度・予定時間は使わず、子の作業から計算した値を画面に出す (<see cref="WorkItemTree"/>)。
/// </remarks>
public sealed record WorkItem
{
    /// <summary>番号 (保存先が決める。0 は、まだ保存していない)</summary>
    public int Id { get; init; }

    /// <summary>親の番号 (0 は最上位)</summary>
    public int ParentId { get; init; }

    /// <summary>種類</summary>
    public WorkItemKind Kind { get; init; } = WorkItemKind.Work;

    /// <summary>兄弟の中の並び (小さいほど先)</summary>
    public int SortOrder { get; init; }

    /// <summary>名前 (必須)</summary>
    public string Name { get; init; } = "";

    /// <summary>備考 (作業ごとの共通の備考。永続)</summary>
    public string Note { get; init; } = "";

    /// <summary>優先度</summary>
    public WorkItemPriority Priority { get; init; } = WorkItemPriority.Normal;

    /// <summary>開始日 (無ければ null)</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>期限日 (無ければ null)</summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>状態</summary>
    public WorkItemStatus Status { get; init; } = WorkItemStatus.Open;

    /// <summary>進捗度 (0 から 100 の 10 刻みの整数。ラベルではなく数値を持つ)</summary>
    public int Progress { get; init; }

    /// <summary>予定時間 (作業ごとの共通の値。無ければ null)</summary>
    public double? PlannedHours { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }
}
