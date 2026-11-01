namespace MmmTool.WorkItems.Core;

/// <summary>状態 (未対応・作業予定・作業中・保留・完了の 5 つ)</summary>
/// <remarks>値はコードマスタ (区分 1)のコード番号と同じ。表示順とは別で、表示順は作業中の次が保留、その次が完了。一度使ったコード番号は、別の意味に使わない。詳細は docs/specs/work-items.md の「コードマスタ」にある。</remarks>
public enum WorkItemStatus
{
    /// <summary>未対応 (既定)</summary>
    Open = 1,

    /// <summary>作業予定 (作業報告の予定に出す)</summary>
    Planned = 2,

    /// <summary>作業中</summary>
    InProgress = 3,

    /// <summary>完了</summary>
    Closed = 4,

    /// <summary>保留</summary>
    /// <remarks>止めている作業。自分の都合でも、他の人の返事待ちでもよく、理由は備考に書く。作業報告の「保留」に出す。</remarks>
    OnHold = 5,
}

/// <summary>状態の分類 (どの状態が、何を意味するかの判断を、1 か所にまとめる)</summary>
/// <remarks>
/// 判断をコード番号の直書きにしないための入口。作業報告・自動入力・案件とグループの状態の計算・保存先の SQL の状態の条件は、ここの集合から決める。
/// 状態を足すときは、ここの集合に書き足す。
/// 「完了」と「未対応」は、それ自体が意味を持つ状態なので、分類にせず <see cref="WorkItemStatus.Closed"/>・<see cref="WorkItemStatus.Open"/> を直接比べる。
/// </remarks>
public static class WorkItemStatuses
{
    /// <summary>作業報告の「予定」に出す状態 (作業予定・作業中)</summary>
    public static IReadOnlyList<WorkItemStatus> ReportPlanned { get; } = [WorkItemStatus.Planned, WorkItemStatus.InProgress];

    /// <summary>作業報告の「保留」に出す状態 (保留)</summary>
    public static IReadOnlyList<WorkItemStatus> ReportOnHold { get; } = [WorkItemStatus.OnHold];

    /// <summary>自動入力の対象にならない状態 (完了・保留)</summary>
    public static IReadOnlyList<WorkItemStatus> AutoEntryStopped { get; } = [WorkItemStatus.Closed, WorkItemStatus.OnHold];

    /// <summary>止めている状態</summary>
    /// <remarks>案件とグループの状態は、完了でない作業がすべてこの状態なら、この状態になる。</remarks>
    public static IReadOnlyList<WorkItemStatus> Stopped { get; } = [WorkItemStatus.OnHold];

    /// <summary>作業報告の「予定」に出す状態か</summary>
    /// <param name="status">状態</param>
    /// <returns>予定に出す状態なら true</returns>
    public static bool IsReportPlanned(this WorkItemStatus status) => ReportPlanned.Contains(status);

    /// <summary>作業報告の「保留」に出す状態か</summary>
    /// <param name="status">状態</param>
    /// <returns>保留に出す状態なら true</returns>
    public static bool IsReportOnHold(this WorkItemStatus status) => ReportOnHold.Contains(status);

    /// <summary>止めている状態か</summary>
    /// <param name="status">状態</param>
    /// <returns>止めている状態なら true</returns>
    public static bool IsStopped(this WorkItemStatus status) => Stopped.Contains(status);

    /// <summary>作業報告の予定と保留を読むときの、状態の条件の中身 (SQL の <c>IN (…)</c> の中)</summary>
    /// <remarks>コード番号の並びで、例は <c>2, 3, 5</c>。列挙の整数だけから作るので、入力は混ざらない。</remarks>
    public static string ReportSqlList => SqlList([.. ReportPlanned, .. ReportOnHold]);

    /// <summary>自動入力の対象にならない状態の、SQL の <c>IN (…)</c> の中身</summary>
    public static string AutoEntryStoppedSqlList => SqlList(AutoEntryStopped);

    /// <summary>止めている状態の、SQL の <c>IN (…)</c> の中身</summary>
    public static string StoppedSqlList => SqlList(Stopped);

    /// <summary>案件・グループの状態 (計算値)を、下の作業の状態から求める</summary>
    /// <param name="statuses">下の作業すべての状態</param>
    /// <returns>作業が無ければ null。すべて未対応なら未対応、すべて完了なら完了。完了でない作業がすべて止めている状態なら、その状態。それ以外は作業中</returns>
    public static WorkItemStatus? ContainerStatus(IReadOnlyCollection<WorkItemStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return null;
        }
        if (statuses.All(s => s == WorkItemStatus.Open))
        {
            return WorkItemStatus.Open;
        }
        if (statuses.All(s => s == WorkItemStatus.Closed))
        {
            return WorkItemStatus.Closed;
        }
        var unfinished = statuses.Where(s => s != WorkItemStatus.Closed).ToList();
        return unfinished.All(IsStopped) ? unfinished[0] : WorkItemStatus.InProgress;
    }

    /// <summary>状態の並びを、SQL の <c>IN (…)</c> の中身にする</summary>
    /// <param name="statuses">状態</param>
    /// <returns>コード番号を「, 」でつなげた文字列</returns>
    private static string SqlList(IEnumerable<WorkItemStatus> statuses) => string.Join(", ", statuses.Select(s => (int)s));
}
