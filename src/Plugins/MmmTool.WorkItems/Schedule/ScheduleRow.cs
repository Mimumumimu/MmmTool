using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Schedule;

/// <summary>スケジュールの表の 1 行 (案件・グループ・作業のどれかを、左の列と、日付の棒に出す形にしたもの)</summary>
/// <param name="key">行の識別 (種類 + 番号。作ったときに決まり、変わらない)</param>
/// <remarks>
/// 表に出している間は同じ行を使い回し、内容が変わったときは <see cref="Update"/> で値だけを書き換える (スクロールの位置を保つため)。
/// 棒の描画に関わる値が変わったときだけ、<see cref="VisualVersion"/> を進めて、セルに描き直させる。
/// 案件・グループの日付・進捗度・実績は、下の作業から計算した値 (<see cref="WorkItemTree"/>・<see cref="ScheduleActuals"/>)。見るだけで、入力はできない。
/// </remarks>
public sealed partial class ScheduleRow(WorkKey key) : ObservableObject, IProjectBlockRow
{
    /// <summary>行の識別 (種類 + 番号)</summary>
    public WorkKey Key { get; } = key;

    /// <summary>案件か</summary>
    public bool IsProject { get; } = key.Kind == WorkRowKind.Project;

    /// <summary>案件の中のグループか</summary>
    public bool IsSubGroup { get; } = key.Kind == WorkRowKind.Group;

    /// <summary>作業か</summary>
    public bool IsWork { get; } = key.Kind == WorkRowKind.Work;

    /// <summary>集計の行か (案件・グループ。下の作業から計算した値を出す)</summary>
    public bool IsGroup { get; } = key.Kind != WorkRowKind.Work;

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

    /// <summary>共通の備考 (作業・グループ・案件の備考で、無ければ空)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    [NotifyPropertyChangedFor(nameof(NoteTip))]
    public partial string Note { get; private set; } = "";

    /// <summary>共通の備考があるか (名前の横の印を出す)</summary>
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// <summary>共通の備考の吹き出しの文 (長い備考は、上限で切る)</summary>
    public string NoteTip => ScheduleNoteText.Shorten(Note);

    /// <summary>備考が無いときの日の備考</summary>
    public static readonly IReadOnlyDictionary<DateOnly, string> NoDayNotes = new Dictionary<DateOnly, string>();

    /// <summary>日付ごとの日の備考 (作業の行だけで、無ければ空)</summary>
    public IReadOnlyDictionary<DateOnly, string> DayNotes { get; private set; } = NoDayNotes;

    /// <summary>担当者の表示名 (作業の行だけで、無ければ空)</summary>
    [ObservableProperty]
    public partial string AssigneeName { get; private set; } = "";

    /// <summary>進捗度の文字 (例: <c>45%</c>。無ければ空)</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>期限を過ぎた日数 (作業だけ。今日で判定。過ぎていなければ 0)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverdue))]
    [NotifyPropertyChangedFor(nameof(OverdueText))]
    public partial int OverdueDays { get; private set; }

    /// <summary>期限切れか (作業だけ。案件・グループには出さない)</summary>
    public bool IsOverdue => IsWork && OverdueDays > 0;

    /// <summary>期限切れの説明 (警告のアイコンに触れたときに出す)</summary>
    public string OverdueText => $"{OverdueDays} 日超過";

    /// <summary>予定の棒の最初の日 (無ければ null)</summary>
    public DateOnly? PlanStart { get; private set; }

    /// <summary>予定の棒の最後の日 (期限日。無ければ null)</summary>
    public DateOnly? PlanEnd { get; private set; }

    /// <summary>実績の棒 (連続した日ごと。日付の順)</summary>
    public IReadOnlyList<ScheduleActualRun> Runs { get; private set; } = [];

    /// <summary>日付の軸 (棒の位置と、共有の図形)</summary>
    public ScheduleAxisVisual? Axis { get; private set; }

    /// <summary>今日 (今日の線の位置)</summary>
    public DateOnly Today { get; private set; }

    /// <summary>棒の描画に関わる値が変わるたびに進む番号 (セルが描き直すきっかけ)</summary>
    [ObservableProperty]
    public partial int VisualVersion { get; private set; }

    /// <summary>行に重ねる色 (案件は青の半透明。作業管理の表と同じ。ほかは重ねない)</summary>
    public Windows.UI.Color? RowTint => IsProject ? Windows.UI.Color.FromArgb(0x2E, 0x4D, 0x8F, 0xE0) : null;

    /// <summary>集計の行か (表の背景を、集計の行の色にする)</summary>
    public bool IsSummary => IsGroup;

    /// <summary>このセルを、入力できるか (見るだけなので、入力できる列は無い)</summary>
    /// <param name="columnKey">列のキー</param>
    /// <returns>常に false</returns>
    public bool CanEdit(string columnKey) => false;

    /// <summary>表示する内容に合わせて、値を書き換える</summary>
    /// <param name="node">木の行 (計算済みの値を持つ)</param>
    /// <param name="level">字下げの段</param>
    /// <param name="hasChildren">子を持つか</param>
    /// <param name="isExpanded">開いているか</param>
    /// <param name="assigneeName">担当者の表示名 (作業の行だけ)</param>
    /// <param name="runs">実績の棒</param>
    /// <param name="dayNotes">日付ごとの日の備考 (作業の行だけで、無ければ空)</param>
    /// <param name="axis">日付の軸</param>
    /// <param name="today">今日</param>
    public void Update(WorkNode node, int level, bool hasChildren, bool isExpanded, string assigneeName, IReadOnlyList<ScheduleActualRun> runs, IReadOnlyDictionary<DateOnly, string> dayNotes, ScheduleAxisVisual axis, DateOnly today)
    {
        Level = level;
        HasChildren = hasChildren;
        IsExpanded = isExpanded;
        Name = node.Name;
        Note = node.Note;
        AssigneeName = assigneeName;

        var summary = node.Summary;
        ProgressText = summary.Progress is { } percent ? $"{percent}%" : "";

        // 棒は、すべての値を書き換えたあとに、1 回だけ描き直させる (途中の値で描き直さないため。期限切れの枠の付け外しも、ここで見る)
        var overdueDays = IsWork ? summary.OverdueDays(today) : 0;
        var changed = PlanStart != summary.StartDate
            || PlanEnd != summary.DueDate
            || !ReferenceEquals(Axis, axis)
            || Today != today
            || OverdueDays != overdueDays
            || !SameRuns(Runs, runs)
            || !SameDayNotes(DayNotes, dayNotes);
        OverdueDays = overdueDays;
        DayNotes = dayNotes;
        PlanStart = summary.StartDate;
        PlanEnd = summary.DueDate;
        Runs = runs;
        Axis = axis;
        Today = today;
        if (changed)
        {
            VisualVersion++;
        }
    }

    /// <summary>日の備考が、同じ内容か</summary>
    /// <param name="a">一方</param>
    /// <param name="b">もう一方</param>
    /// <returns>同じ (日付と備考が、すべて等しい)なら true</returns>
    private static bool SameDayNotes(IReadOnlyDictionary<DateOnly, string> a, IReadOnlyDictionary<DateOnly, string> b)
        => ReferenceEquals(a, b) || (a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var text) && text == pair.Value));

    /// <summary>実績の棒が、同じ内容か</summary>
    /// <param name="a">一方</param>
    /// <param name="b">もう一方</param>
    /// <returns>同じ (開始日と、日ごとの時間が、すべて等しい)なら true</returns>
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
