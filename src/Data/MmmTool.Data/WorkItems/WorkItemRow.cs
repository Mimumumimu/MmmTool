using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>
/// DB の <c>WorkItem</c> (作業)の 1 行 (列と 1 対 1)。アプリの型 <see cref="WorkItem"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>
/// 日付の「ない」は <c>9999-12-31</c> (開始日だけ <c>0001-01-01</c>)、予定時間の「ない」は 0 (NULL は使わない。理由は docs/specs/database.md)。
/// 変換は <see cref="FromWorkItem"/> と <see cref="ToWorkItem"/>。作成・更新の日時と作成者・更新者は、Repository が入れる。
/// </remarks>
public sealed record WorkItemRow
{
    /// <summary>主キー。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (ユーザーの番号)</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (ユーザーの番号)</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>属する案件 (<c>WorkProject.Id</c>)</summary>
    public int ProjectId { get; init; }

    /// <summary>親のグループ (<c>WorkGroup.Id</c>。0 は案件の直下)</summary>
    public int GroupId { get; init; }

    /// <summary>兄弟の中の並び (グループと作業にまたがる 1 つの数)</summary>
    public int SortOrder { get; init; }

    /// <summary>名前</summary>
    public string Name { get; init; } = "";

    /// <summary>担当者 (<c>AppUser.Id</c>。0 は未割り当て)</summary>
    public int AssigneeUserId { get; init; }

    /// <summary>備考 (無いときは空文字)</summary>
    public string Note { get; init; } = "";

    /// <summary>優先度のコード番号 (コードマスタの区分「優先度」。高 = 1・中 = 2・低 = 3)</summary>
    public int PriorityCodeNo { get; init; } = (int)WorkItemPriority.Normal;

    /// <summary>開始日 (ない日は <c>0001-01-01</c>)</summary>
    public DateOnly StartDate { get; init; } = WorkRowValues.NoStartDate;

    /// <summary>期限日 (ない日は <c>9999-12-31</c>)</summary>
    public DateOnly DueDate { get; init; } = WorkRowValues.NoDate;

    /// <summary>リンク (無いときは空文字)</summary>
    public string Link { get; init; } = "";

    /// <summary>作業報告の予定に含めないか</summary>
    public bool PlanExcluded { get; init; }

    /// <summary>状態のコード番号 (コードマスタの区分「状態」。未対応 = 1・作業予定 = 2・作業中 = 3・完了 = 4)</summary>
    public int StatusCodeNo { get; init; } = (int)WorkItemStatus.Open;

    /// <summary>進捗度のコード番号 (コードマスタの区分「進捗度」。%の値は、コードマスタの数値)</summary>
    public int ProgressCodeNo { get; init; } = WorkCodes.DefaultProgressCodeNo;

    /// <summary>予定時間 (0 は予定なし)</summary>
    public decimal PlannedHours { get; init; }

    /// <summary>完了日 (未完了は <c>9999-12-31</c>)</summary>
    public DateOnly CompletedDate { get; init; } = WorkRowValues.NoDate;

    /// <summary>実績の累計 (記録を保存するたびに、保存先が計算し直して書く)</summary>
    public decimal ActualTotalHours { get; init; }

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="item">作業</param>
    /// <returns>DB の行。作成・更新の日時と作成者・更新者は決めない (Repository が入れる)</returns>
    /// <exception cref="ArgumentException">DB に入れられない値 (長さ・範囲の外、「ない」を表す日付など)。</exception>
    public static WorkItemRow FromWorkItem(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.ProjectId <= 0 || item.GroupId < 0 || item.SortOrder < 0 || item.AssigneeUserId < 0)
        {
            throw new ArgumentException("作業の案件・親・並びの番号が正しくありません。", nameof(item));
        }
        if ((int)item.Priority < 1 || (int)item.Status < 1 || item.ProgressCodeNo < 1)
        {
            throw new ArgumentException("作業の優先度・状態・進捗度のコード番号が正しくありません (1 以上)。", nameof(item));
        }
        return new WorkItemRow
        {
            Id = item.Id,
            IsDeleted = item.IsDeleted,
            ProjectId = item.ProjectId,
            GroupId = item.GroupId,
            SortOrder = item.SortOrder,
            Name = WorkRowValues.CheckText(item.Name, "作業の名前", WorkRowValues.NameMax, required: true),
            AssigneeUserId = item.AssigneeUserId,
            Note = WorkRowValues.CheckText(item.Note, "作業の備考", WorkRowValues.NoteMax),
            PriorityCodeNo = (int)item.Priority,
            StartDate = WorkRowValues.FromDate(item.StartDate, "開始日", WorkRowValues.NoStartDate),
            DueDate = WorkRowValues.FromDate(item.DueDate, "期限日"),
            Link = WorkRowValues.CheckText(item.Link, "作業のリンク", WorkRowValues.LinkMax),
            PlanExcluded = item.PlanExcluded,
            StatusCodeNo = (int)item.Status,
            ProgressCodeNo = item.ProgressCodeNo,
            PlannedHours = item.PlannedHours is { } planned ? WorkRowValues.FromHours(planned, "予定時間", WorkItemLimits.PlannedHoursMax) : 0,
            CompletedDate = WorkRowValues.FromDate(item.CompletedDate, "完了日"),
            ActualTotalHours = WorkRowValues.FromHours(item.ActualTotalHours, "実績の累計", 9_999_999_999),
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>作業</returns>
    public WorkItem ToWorkItem() => new()
    {
        Id = Id,
        ProjectId = ProjectId,
        GroupId = GroupId,
        SortOrder = SortOrder,
        Name = Name,
        AssigneeUserId = AssigneeUserId,
        Note = Note,
        Priority = (WorkItemPriority)PriorityCodeNo,
        StartDate = WorkRowValues.ToDate(StartDate, WorkRowValues.NoStartDate),
        DueDate = WorkRowValues.ToDate(DueDate),
        Link = Link,
        PlanExcluded = PlanExcluded,
        Status = (WorkItemStatus)StatusCodeNo,
        ProgressCodeNo = ProgressCodeNo,
        PlannedHours = PlannedHours > 0 ? WorkRowValues.ToHours(PlannedHours) : null,
        CompletedDate = WorkRowValues.ToDate(CompletedDate),
        ActualTotalHours = WorkRowValues.ToHours(ActualTotalHours),
        IsDeleted = IsDeleted,
        UpdatedAt = UpdatedAt,
    };
}
