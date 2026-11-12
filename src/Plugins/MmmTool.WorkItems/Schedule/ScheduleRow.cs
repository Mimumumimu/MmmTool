using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.CodeMaster.Core;
using MmmTool.WorkItems.Core;
using MmmTool.WorkItems.Main;

namespace MmmTool.WorkItems.Schedule;

/// <summary>スケジュールの表の 1 行</summary>
/// <param name="key">
/// 行の識別で、種類 + 番号。
/// 作ったときに決まり、変わらない。
/// </param>
/// <remarks>
/// 案件・グループ・作業のどれかを左の列と日付の棒に出す形にしたもの。
/// 表に出している間は同じ行を使い回し、内容が変わったときは <see cref="Update"/> で値だけを書き換える。
/// スクロールの位置を保つため。
/// 棒の描画に関わる値が変わったときだけ、<see cref="VisualVersion"/> を進めて、セルに描き直させる。
/// 案件・グループの日付・状態・進捗度・実績は下の作業から計算した値で、<see cref="WorkItemTree"/>・<see cref="ScheduleActuals"/> が作る。
/// 見るだけで、入力はできない。
/// </remarks>
public sealed partial class ScheduleRow(WorkKey key) : ObservableObject, IProjectBlockRow
{
    /// <summary>行の識別</summary>
    public WorkKey Key { get; } = key;

    /// <summary>案件か</summary>
    public bool IsProject { get; } = key.Kind == WorkRowKind.Project;

    /// <summary>案件の中のグループか</summary>
    public bool IsSubGroup { get; } = key.Kind == WorkRowKind.Group;

    /// <summary>作業か</summary>
    public bool IsWork { get; } = key.Kind == WorkRowKind.Work;

    /// <summary>集計の行か</summary>
    /// <remarks>案件・グループで、下の作業から計算した値を出す。</remarks>
    public bool IsGroup { get; } = key.Kind != WorkRowKind.Work;

    /// <summary>総合計の行か</summary>
    /// <remarks>表示している案件を合わせた実績を出す行で、表の上に固定する。</remarks>
    public bool IsTotal { get; private init; }

    /// <summary>案件・グループの行か</summary>
    /// <remarks>
    /// 総合計の行は含まない
    /// 計算値の色で出すセルを決める
    /// </remarks>
    public bool IsComputedRow => IsGroup && !IsTotal;

    /// <summary>総合計の行を作る</summary>
    /// <returns>
    /// 総合計の行
    /// 値は <see cref="UpdateTotal"/> で入れる
    /// </returns>
    /// <remarks>
    /// 案件・グループ・作業のどれでもないので、行の識別は既定値のままで、使わない。
    /// 集計の行として出す。
    /// </remarks>
    public static ScheduleRow CreateTotal() => new(default) { IsTotal = true, Name = "総合計" };

    /// <summary>字下げの段</summary>
    [ObservableProperty]
    public partial int Level { get; private set; }

    /// <summary>子を持つか</summary>
    [ObservableProperty]
    public partial bool HasChildren { get; private set; }

    /// <summary>開いているか</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; private set; }

    /// <summary>名前</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>共通の備考</summary>
    /// <remarks>作業・グループ・案件の備考で、無ければ空。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    [NotifyPropertyChangedFor(nameof(NoteTip))]
    public partial string Note { get; private set; } = "";

    /// <summary>共通の備考があるか</summary>
    /// <remarks>名前の横の印を出す</remarks>
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// <summary>共通の備考の吹き出しの文</summary>
    /// <remarks>長い備考は上限で切る</remarks>
    public string NoteTip => ScheduleNoteText.Shorten(Note);

    /// <summary>備考が無いときの日の備考</summary>
    public static readonly IReadOnlyDictionary<DateOnly, string> NoDayNotes = new Dictionary<DateOnly, string>();

    /// <summary>日付ごとの日の備考</summary>
    /// <remarks>作業の行だけで、無ければ空。</remarks>
    public IReadOnlyDictionary<DateOnly, string> DayNotes { get; private set; } = NoDayNotes;

    /// <summary>担当者の表示名</summary>
    /// <remarks>作業の行だけで、無ければ空。</remarks>
    [ObservableProperty]
    public partial string AssigneeName { get; private set; } = "";

    /// <summary>状態の名前</summary>
    /// <remarks>
    /// 例: <c>作業中</c>で、無ければ空。
    /// 作業管理の表と同じ名前。
    /// 案件・グループは下の作業から計算した値。
    /// </remarks>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    /// <summary>進捗度の文字で、例は 45%</summary>
    /// <remarks>無ければ空</remarks>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>実績の時間の合計の文字</summary>
    /// <remarks>
    /// 例: <c>12.5</c>。
    /// 作業管理の表の「実績 (累計)」と同じ値で、実績が無ければ空。
    /// 案件・グループは下の作業の合計。
    /// </remarks>
    [ObservableProperty]
    public partial string TotalHoursText { get; private set; } = "";

    /// <summary>期限を過ぎた日数で、作業だけ</summary>
    /// <remarks>
    /// 今日で判定する
    /// 過ぎていなければ 0
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverdue))]
    [NotifyPropertyChangedFor(nameof(OverdueText))]
    public partial int OverdueDays { get; private set; }

    /// <summary>期限切れか</summary>
    /// <remarks>作業だけで、案件・グループには出さない。</remarks>
    public bool IsOverdue => IsWork && OverdueDays > 0;

    /// <summary>期限切れの説明</summary>
    /// <remarks>警告のアイコンに触れたときに出す</remarks>
    public string OverdueText => $"{OverdueDays} 日超過";

    /// <summary>予定の棒の最初の日</summary>
    public DateOnly? PlanStart { get; private set; }

    /// <summary>予定の棒の最後の日で、期限日</summary>
    public DateOnly? PlanEnd { get; private set; }

    /// <summary>完了日</summary>
    /// <remarks>
    /// 完了した行だけが持つ。
    /// 案件・グループはすべて完了したときに、下の作業の完了日の最大値を持つ。
    /// 未完了の行は null。
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowTint))]
    public partial DateOnly? CompletedDate { get; private set; }

    /// <summary>連続した日ごとの日付順の実績の棒</summary>
    public IReadOnlyList<ScheduleActualRun> Runs { get; private set; } = [];

    /// <summary>日付の軸</summary>
    public ScheduleAxisVisual? Axis { get; private set; }

    /// <summary>今日</summary>
    public DateOnly Today { get; private set; }

    /// <summary>棒の描画に関わる値が変わるたびに進む番号</summary>
    [ObservableProperty]
    public partial int VisualVersion { get; private set; }

    /// <summary>行に重ねる色</summary>
    /// <remarks>
    /// 案件は青の半透明、完了した作業は灰色の半透明で、作業管理の表と同じ色。
    /// ほかは重ねない。
    /// 作業管理の表と違い、今日完了した作業にも重ねる。
    /// この画面に入力日は無いため。
    /// </remarks>
    public Windows.UI.Color? RowTint => IsProject
        ? WorkRowTints.Project
        : IsWork && CompletedDate is not null ? WorkRowTints.Finished : null;

    /// <summary>集計の行か</summary>
    /// <remarks>表の背景を集計の行の色にする</remarks>
    public bool IsSummary => IsGroup;

    /// <summary>このセルを入力できるか</summary>
    /// <param name="columnKey">列のキー</param>
    /// <returns>常に false</returns>
    /// <remarks>見るだけなので、入力できる列は無い。</remarks>
    public bool CanEdit(string columnKey) => false;

    /// <summary>表示する内容に合わせて値を書き換える</summary>
    /// <param name="node">木の行 (計算済みの値を持つ)</param>
    /// <param name="level">字下げの段</param>
    /// <param name="hasChildren">子を持つか</param>
    /// <param name="isExpanded">開いているか</param>
    /// <param name="assigneeName">担当者の表示名 (作業の行だけ)</param>
    /// <param name="codes">コードマスタ (状態の名前を引く)</param>
    /// <param name="runs">実績の棒</param>
    /// <param name="dayNotes">日付ごとの日の備考 (作業の行だけで、無ければ空)。</param>
    /// <param name="axis">日付の軸</param>
    /// <param name="today">今日</param>
    public void Update(WorkNode node, int level, bool hasChildren, bool isExpanded, string assigneeName, CodeBook codes, IReadOnlyList<ScheduleActualRun> runs, IReadOnlyDictionary<DateOnly, string> dayNotes, ScheduleAxisVisual axis, DateOnly today)
    {
        Level = level;
        HasChildren = hasChildren;
        IsExpanded = isExpanded;
        Name = node.Name;
        Note = node.Note;
        AssigneeName = assigneeName;

        var summary = node.Summary;
        StatusText = summary.Status is { } status ? WorkCodes.Display(codes, WorkCodes.StatusCategory, (int)status) : "";
        ProgressText = summary.Progress is { } percent ? $"{percent}%" : "";
        TotalHoursText = WorkItemRow.FormatHours(summary.ActualTotal > 0 ? summary.ActualTotal : null);

        // 棒は、すべての値を書き換えたあとに、1 回だけ描き直させる。途中の値で描き直さないためで、期限切れの枠の付け外しも、ここで見る
        var overdueDays = IsWork ? summary.OverdueDays(today) : 0;
        var completedDate = summary.IsFinished ? summary.CompletedDate : null;
        var changed = PlanStart != summary.StartDate
            || PlanEnd != summary.DueDate
            || CompletedDate != completedDate
            || !ReferenceEquals(Axis, axis)
            || Today != today
            || OverdueDays != overdueDays
            || !SameRuns(Runs, runs)
            || !SameDayNotes(DayNotes, dayNotes);
        OverdueDays = overdueDays;
        DayNotes = dayNotes;
        PlanStart = summary.StartDate;
        PlanEnd = summary.DueDate;
        CompletedDate = completedDate;
        Runs = runs;
        Axis = axis;
        Today = today;
        if (changed)
        {
            VisualVersion++;
        }
    }

    /// <summary>総合計の行の値を書き換える</summary>
    /// <param name="totalHours">表示している案件の、実績の時間の合計 (全期間)。</param>
    /// <param name="runs">表示している案件の実績を、日ごとに足した実績の棒。</param>
    /// <param name="axis">日付の軸</param>
    /// <param name="today">今日</param>
    /// <remarks>
    /// 出すのは実績の合計と、日ごとの実績の棒だけ。
    /// 予定の棒・状態・進捗・完了日は持たない。
    /// </remarks>
    public void UpdateTotal(double totalHours, IReadOnlyList<ScheduleActualRun> runs, ScheduleAxisVisual axis, DateOnly today)
    {
        TotalHoursText = WorkItemRow.FormatHours(totalHours > 0 ? totalHours : null);
        var changed = !ReferenceEquals(Axis, axis) || Today != today || !SameRuns(Runs, runs);
        Runs = runs;
        Axis = axis;
        Today = today;
        if (changed)
        {
            VisualVersion++;
        }
    }

    /// <summary>日の備考が同じ内容か</summary>
    /// <param name="a">一方</param>
    /// <param name="b">もう一方</param>
    /// <returns>同じ (日付と備考がすべて等しい)なら true</returns>
    private static bool SameDayNotes(IReadOnlyDictionary<DateOnly, string> a, IReadOnlyDictionary<DateOnly, string> b)
        => ReferenceEquals(a, b) || (a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var text) && text == pair.Value));

    /// <summary>実績の棒が同じ内容か</summary>
    /// <param name="a">一方</param>
    /// <param name="b">もう一方</param>
    /// <returns>同じ (開始日と、日ごとの時間がすべて等しい)なら true。</returns>
    private static bool SameRuns(IReadOnlyList<ScheduleActualRun> a, IReadOnlyList<ScheduleActualRun> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Start != b[i].Start || !a[i].Hours.SequenceEqual(b[i].Hours))
            {
                return false;
            }
        }
        return true;
    }
}
