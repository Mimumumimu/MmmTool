using MmmSdk.Core.Components.DaysOfWeek;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>DB の WorkItemの 1 行で、列と 1 対 1 に対応する</summary>
/// <remarks>
/// アプリの型 WorkItem とは別の DB の形のまま持つ。
/// 日付の「ない」は <c>9999-12-31</c> で、開始日だけ <c>0001-01-01</c>。
/// 予定時間の「ない」は 0。
/// NULL は使わない。
/// 理由は docs/specs/database.md にある。
/// 変換は <see cref="FromWorkItem"/> と <see cref="ToWorkItem"/>。
/// 作成・更新の日時と作成者・更新者は Repository が入れる。
/// </remarks>
public sealed record WorkItemRow
{
    /// <summary>主キー</summary>
    /// <remarks>
    /// DB が採番する
    /// 0 は新規
    /// </remarks>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>属する案件</summary>
    public int ProjectId { get; init; }

    /// <summary>親のグループで、WorkGroup.Id</summary>
    /// <remarks>0 は案件の直下</remarks>
    public int GroupId { get; init; }

    /// <summary>兄弟の中の並び</summary>
    public int SortOrder { get; init; }

    /// <summary>名前</summary>
    public string Name { get; init; } = "";

    /// <summary>担当者で、AppUser.Id</summary>
    /// <remarks>0 は未割り当て</remarks>
    public int AssigneeUserId { get; init; }

    /// <summary>備考</summary>
    public string Note { get; init; } = "";

    /// <summary>優先度のコード番号で、コードマスタの区分「優先度」</summary>
    /// <remarks>高 = 1・中 = 2・低 = 3</remarks>
    public int PriorityCodeNo { get; init; } = (int)WorkItemPriority.Normal;

    /// <summary>開始日</summary>
    public DateOnly StartDate { get; init; } = WorkRowValues.NoStartDate;

    /// <summary>期限日</summary>
    public DateOnly DueDate { get; init; } = WorkRowValues.NoDate;

    /// <summary>リンク</summary>
    public string Link { get; init; } = "";

    /// <summary>作業報告のどの部分から外すか</summary>
    /// <remarks>0 = 外さない・1 = 予定から外す・2 = 実績と予定から外す</remarks>
    public byte ReportExclusion { get; init; }

    /// <summary>状態のコード番号で、コードマスタの区分「状態」</summary>
    /// <remarks>未対応 = 1・作業予定 = 2・作業中 = 3・完了 = 4</remarks>
    public int StatusCodeNo { get; init; } = (int)WorkItemStatus.Open;

    /// <summary>進捗度のコード番号で、コードマスタの区分「進捗度」</summary>
    /// <remarks>%の値はコードマスタの数値</remarks>
    public int ProgressCodeNo { get; init; } = WorkCodes.DefaultProgressCodeNo;

    /// <summary>予定時間</summary>
    public decimal PlannedHours { get; init; }

    /// <summary>完了日</summary>
    public DateOnly CompletedDate { get; init; } = WorkRowValues.NoDate;

    /// <summary>実績の累計</summary>
    /// <remarks>記録を保存するたびに保存先が計算し直して書く</remarks>
    public decimal ActualTotalHours { get; init; }

    /// <summary>自動入力の時間</summary>
    /// <remarks>0 は自動入力しない</remarks>
    public decimal AutoHours { get; init; }

    /// <summary>自動入力する曜日</summary>
    /// <remarks>
    /// 月 = 1 から日 = 64 の足し算の数
    /// 0 と 127 はどちらも毎日
    /// </remarks>
    public byte AutoWeekdays { get; init; }

    /// <summary>アプリの型から DB の行にする</summary>
    /// <param name="item">作業</param>
    /// <returns>
    /// DB の行
    /// 作成・更新の日時と作成者・更新者は決めない (Repository が入れる)
    /// </returns>
    /// <exception cref="ArgumentException">DB に入れられない値で、長さ・範囲の外、「ない」を表す日付など。</exception>
    public static WorkItemRow FromWorkItem(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.ProjectId <= 0 || item.GroupId < 0 || item.SortOrder < 0 || item.AssigneeUserId < 0)
        {
            throw new ArgumentException("作業の案件・親・並びの番号が正しくありません。", nameof(item));
        }
        if ((int)item.Priority < 1 || (int)item.Status < 1 || item.ProgressCodeNo < 1)
        {
            throw new ArgumentException("作業の優先度・状態・進捗度のコード番号が正しくありません。1 以上にしてください。", nameof(item));
        }
        if (!Enum.IsDefined(item.ReportExclusion))
        {
            throw new ArgumentException("作業の報告から外す範囲の値が正しくありません。", nameof(item));
        }
        if ((int)item.AutoWeekdays is < 0 or > (int)Weekdays.All)
        {
            throw new ArgumentException($"自動入力の曜日 {(int)item.AutoWeekdays} は、0 から 127 の範囲ではありません。", nameof(item));
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
            ReportExclusion = (byte)item.ReportExclusion,
            StatusCodeNo = (int)item.Status,
            ProgressCodeNo = item.ProgressCodeNo,
            PlannedHours = item.PlannedHours is { } planned ? WorkRowValues.FromHours(planned, "予定時間", WorkItemLimits.PlannedHoursMax) : 0,
            CompletedDate = WorkRowValues.FromDate(item.CompletedDate, "完了日"),
            ActualTotalHours = WorkRowValues.FromHours(item.ActualTotalHours, "実績の累計", 9_999_999_999),
            AutoHours = WorkRowValues.FromHours(item.AutoHours, "自動入力の時間", WorkItemLimits.DailyHoursMax),
            AutoWeekdays = (byte)item.AutoWeekdays,
        };
    }

    /// <summary>DB の行からアプリの型にする</summary>
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
        ReportExclusion = (WorkReportExclusion)ReportExclusion,
        Status = (WorkItemStatus)StatusCodeNo,
        ProgressCodeNo = ProgressCodeNo,
        PlannedHours = PlannedHours > 0 ? WorkRowValues.ToHours(PlannedHours) : null,
        CompletedDate = WorkRowValues.ToDate(CompletedDate),
        ActualTotalHours = WorkRowValues.ToHours(ActualTotalHours),
        AutoHours = WorkRowValues.ToHours(AutoHours),
        AutoWeekdays = (Weekdays)AutoWeekdays,
        IsDeleted = IsDeleted,
        UpdatedAt = UpdatedAt,
    };
}
