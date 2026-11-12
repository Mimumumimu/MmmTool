using MmmSdk.Core.Components.DaysOfWeek;

namespace MmmTool.WorkItems.Core;

/// <summary>作業</summary>
/// <remarks>
/// 入力する行で、子を持たない。
/// 保存するデータで、保存先に依存しない。
/// 日付・予定時間・完了日の「無い」は null で、保存先では開始日は <c>0001-01-01</c>、期限日・完了日は <c>9999-12-31</c>、予定時間は 0 に置き換える。
/// </remarks>
public sealed record WorkItem
{
    /// <summary>番号</summary>
    /// <remarks>保存先が決め、0 はまだ保存していない。</remarks>
    public int Id { get; init; }

    /// <summary>属する案件の番号</summary>
    public int ProjectId { get; init; }

    /// <summary>親のグループの番号</summary>
    public int GroupId { get; init; }

    /// <summary>兄弟の中の並び</summary>
    /// <remarks>小さいほど先で、グループと作業にまたがる 1 つの数。</remarks>
    public int SortOrder { get; init; }

    /// <summary>名前</summary>
    public string Name { get; init; } = "";

    /// <summary>担当者</summary>
    /// <remarks>
    /// ユーザーの番号で、0 は未割り当て。
    /// 作った人が既定の担当になる。
    /// </remarks>
    public int AssigneeUserId { get; init; }

    /// <summary>リンク</summary>
    /// <remarks>URL・フォルダ・ファイルのパスで、無いときは空文字。</remarks>
    public string Link { get; init; } = "";

    /// <summary>作業報告のどの部分から外すか</summary>
    /// <remarks>
    /// 定例の会議など、報告に書かない作業に付ける。
    /// <see cref="WorkReportExclusion.Plan"/> は予定だけを外し、実績には工数を記入した日や完了にした日があれば出す。
    /// </remarks>
    public WorkReportExclusion ReportExclusion { get; init; }

    /// <summary>自動入力の時間</summary>
    /// <remarks>
    /// 0 は自動入力しない。
    /// 0 より大きいと、その時間を、システムの今日の記録に入れる。
    /// </remarks>
    public double AutoHours { get; init; }

    /// <summary>自動入力する曜日</summary>
    /// <remarks>
    /// 1 つも選んでいないときと、7 つ全部のときは毎日。
    /// 判定は <see cref="WeekdaySet.IsEveryday"/>。
    /// </remarks>
    public Weekdays AutoWeekdays { get; init; }

    /// <summary>備考</summary>
    /// <remarks>作業ごとの共通の備考で、永続。</remarks>
    public string Note { get; init; } = "";

    /// <summary>優先度</summary>
    public WorkItemPriority Priority { get; init; } = WorkItemPriority.Normal;

    /// <summary>開始日</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>期限日</summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>状態</summary>
    public WorkItemStatus Status { get; init; } = WorkItemStatus.Open;

    /// <summary>進捗度のコード番号</summary>
    /// <remarks>コードマスタの区分「進捗度」のコードで、%の値はコードマスタの数値。</remarks>
    public int ProgressCodeNo { get; init; } = WorkCodes.DefaultProgressCodeNo;

    /// <summary>予定時間</summary>
    /// <remarks>作業ごとの共通の値で、無ければ null。</remarks>
    public double? PlannedHours { get; init; }

    /// <summary>完了日</summary>
    /// <remarks>
    /// 状態が完了に変わった入力日で、内部の値。
    /// 完了でなければ null。
    /// </remarks>
    public DateOnly? CompletedDate { get; init; }

    /// <summary>実績の累計</summary>
    /// <remarks>その作業の全員のすべての日の実績の合計で、記録を保存するたびに保存先が計算し直して書く。</remarks>
    public double ActualTotalHours { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>完了した作業か</summary>
    /// <remarks>
    /// 状態が完了で、完了日がある作業。
    /// 状態が正で、完了日があっても状態が完了でなければ未完了。
    /// 完了日が無い行 (手で直したデータなど)も未完了として扱い、隠さない。
    /// </remarks>
    public bool IsFinished => Status == WorkItemStatus.Closed && CompletedDate is not null;
}
