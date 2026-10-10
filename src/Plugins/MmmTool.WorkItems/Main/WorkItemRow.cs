using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.WinUI.Controls;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Main;

/// <summary>表の 1 行 (グループまたは作業を、表の列に出す形にしたもの)</summary>
/// <param name="id">行の番号</param>
/// <param name="kind">種類 (作ったときに決まり、変わらない)</param>
/// <remarks>
/// 表に出している間は同じ行を使い回し、内容が変わったときは <see cref="Update"/> で値だけを書き換える (入力欄の位置・フォーカスを保つため)。
/// グループの日付・予定時間・実績・進捗度は、下の作業から計算した値の表示 (入力はできない)。
/// </remarks>
public sealed partial class WorkItemRow(int id, WorkItemKind kind) : ObservableObject, ITreeGridRow
{
    /// <summary>行の番号</summary>
    public int Id { get; } = id;

    /// <summary>種類</summary>
    public WorkItemKind Kind { get; } = kind;

    /// <summary>グループの行か</summary>
    public bool IsGroup { get; } = kind == WorkItemKind.Group;

    /// <summary>作業の行か</summary>
    public bool IsWork { get; } = kind == WorkItemKind.Work;

    /// <summary>字下げの段</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProject))]
    [NotifyPropertyChangedFor(nameof(IsSubGroup))]
    [NotifyPropertyChangedFor(nameof(RowTint))]
    public partial int Level { get; private set; }

    /// <summary>案件か (最上位のグループ)</summary>
    public bool IsProject => IsGroup && Level == 0;

    /// <summary>行に重ねる色 (案件だけ、青の半透明。グループ・作業は、重ねない)</summary>
    public Windows.UI.Color? RowTint => IsProject ? Windows.UI.Color.FromArgb(0x2E, 0x4D, 0x8F, 0xE0) : null;

    /// <summary>案件の中のグループか</summary>
    public bool IsSubGroup => IsGroup && Level > 0;

    /// <summary>子を持つか</summary>
    [ObservableProperty]
    public partial bool HasChildren { get; private set; }

    /// <summary>開いているか</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; private set; }

    /// <summary>名前</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>備考 (全文)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteFirstLine))]
    public partial string Note { get; private set; } = "";

    /// <summary>備考の先頭の 1 行 (表のセルに出す)</summary>
    public string NoteFirstLine => FirstLine(Note);

    /// <summary>優先度の表示 (作業のみ)</summary>
    [ObservableProperty]
    public partial string PriorityText { get; private set; } = "";

    /// <summary>状態の表示 (作業のみ)</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    /// <summary>進捗度の表示 (作業はラベルつきの選択肢、グループは計算値)</summary>
    [ObservableProperty]
    public partial string ProgressCellText { get; private set; } = "";

    /// <summary>予定が空のとき、入力の手がかりの文字を出すか (作業のみ)</summary>
    public bool PlannedHintVisible => IsWork && PlannedText.Length == 0;

    /// <summary>入力日の実績が空のとき、入力の手がかりの文字を出すか (作業のみ)</summary>
    public bool ActualDayHintVisible => IsWork && ActualDayText.Length == 0;

    /// <summary>優先度の選択位置</summary>
    [ObservableProperty]
    public partial int PriorityIndex { get; private set; }

    /// <summary>状態の選択位置</summary>
    [ObservableProperty]
    public partial int StatusIndex { get; private set; }

    /// <summary>進捗度の選択位置 (0 から 10。10 刻み)</summary>
    [ObservableProperty]
    public partial int ProgressIndex { get; private set; }

    /// <summary>進捗度の選択肢 (ラベルつき)</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> ProgressOptions { get; private set; } = [];

    /// <summary>グループの進捗度の表示 (計算値。無ければ空)</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>開始日 (<c>yyyy/MM/dd</c>。無ければ空。グループは計算値)</summary>
    [ObservableProperty]
    public partial string StartText { get; private set; } = "";

    /// <summary>開始日 (日付の選択欄の値。無ければ null)</summary>
    [ObservableProperty]
    public partial DateTimeOffset? StartValue { get; private set; }

    /// <summary>期限日 (日付の選択欄の値。無ければ null)</summary>
    [ObservableProperty]
    public partial DateTimeOffset? DueValue { get; private set; }

    /// <summary>期限日 (<c>yyyy/MM/dd</c>。無ければ空。グループは計算値)</summary>
    [ObservableProperty]
    public partial string DueText { get; private set; } = "";

    /// <summary>予定時間 (無ければ空。グループは計算値)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlannedHintVisible))]
    public partial string PlannedText { get; private set; } = "";

    /// <summary>実績の累計 (0 なら空)</summary>
    [ObservableProperty]
    public partial string ActualTotalText { get; private set; } = "";

    /// <summary>入力日の実績 (0 なら空。グループは計算値)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActualDayHintVisible))]
    public partial string ActualDayText { get; private set; } = "";

    /// <summary>入力日の備考 (全文)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DayNoteFirstLine))]
    public partial string DayNote { get; private set; } = "";

    /// <summary>入力日の備考の先頭の 1 行 (表のセルに出す)</summary>
    public string DayNoteFirstLine => FirstLine(DayNote);

    /// <summary>行の内容を書き換える</summary>
    /// <param name="node">木構造の行 (行と集計値)</param>
    /// <param name="level">字下げの段</param>
    /// <param name="isExpanded">開いているか</param>
    /// <param name="dayRecord">入力日の記録 (作業のみ。無ければ null)</param>
    /// <param name="progressOptions">進捗度の選択肢</param>
    /// <param name="labels">進捗度のラベル (グループの進捗度の表示に使う)</param>
    public void Update(
        WorkItemNode node,
        int level,
        bool isExpanded,
        WorkRecord? dayRecord,
        IReadOnlyList<string> progressOptions,
        IReadOnlyDictionary<int, string> labels)
    {
        var item = node.Item;
        var summary = node.Summary;

        Level = level;
        HasChildren = node.Children.Count > 0;
        IsExpanded = isExpanded;
        Name = item.Name;
        Note = item.Note;
        ProgressOptions = progressOptions;
        StartText = FormatDate(summary.StartDate);
        DueText = FormatDate(summary.DueDate);
        StartValue = ToValue(summary.StartDate);
        DueValue = ToValue(summary.DueDate);
        PlannedText = FormatHours(summary.PlannedHours);
        ActualTotalText = FormatHours(summary.ActualTotal > 0 ? summary.ActualTotal : null);

        if (IsWork)
        {
            PriorityIndex = Math.Max(0, WorkItemNames.Priorities.ToList().IndexOf(item.Priority));
            StatusIndex = Math.Max(0, WorkItemNames.Statuses.ToList().IndexOf(item.Status));
            ProgressIndex = Math.Clamp(item.Progress / 10, 0, 10);
            PriorityText = WorkItemOptions.Priorities[PriorityIndex];
            StatusText = WorkItemOptions.Statuses[StatusIndex];
            ProgressCellText = progressOptions.Count > ProgressIndex ? progressOptions[ProgressIndex] : "";
            ActualDayText = FormatHours(dayRecord is { Hours: > 0 } ? dayRecord.Hours : null);
            DayNote = dayRecord?.Note ?? "";
        }
        else
        {
            ProgressText = summary.Progress is { } progress ? WorkProgress.Format(progress, labels) : "";
            ProgressCellText = ProgressText;
            ActualDayText = FormatHours(summary.ActualDay > 0 ? summary.ActualDay : null);
        }
    }

    /// <summary>集計の行か (グループ・案件は、下の作業から計算した値だけを出す)</summary>
    public bool IsSummary => IsGroup;

    /// <summary>このセルを、入力できるか</summary>
    /// <param name="columnKey">列のキー</param>
    /// <returns>名前は、グループも作業も入力できる。ほかの列は、作業だけ (グループは計算値)</returns>
    public bool CanEdit(string columnKey) => columnKey == "Name" || IsWork;

    /// <summary>入力欄の表示を、今の値に戻す</summary>
    /// <remarks>入力が受け入れられなかったとき、入力欄に残った入力途中の文字を、保存されている値に戻すために使う。</remarks>
    public void Revert() => OnPropertyChanged(string.Empty);

    /// <summary>日付を、表の表示にする</summary>
    /// <param name="date">日付 (無ければ null)</param>
    /// <returns><c>yyyy/MM/dd</c>。無ければ空</returns>
    private static string FormatDate(DateOnly? date) => date?.ToString("yyyy/MM/dd") ?? "";

    /// <summary>日付を、日付の選択欄の値にする</summary>
    /// <param name="date">日付 (無ければ null)</param>
    /// <returns>その日の 0 時 (この PC のタイムゾーン)。無ければ null</returns>
    private static DateTimeOffset? ToValue(DateOnly? date) => date is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : null;

    /// <summary>時間を、表の表示にする</summary>
    /// <param name="hours">時間 (無ければ null)</param>
    /// <returns>小数 2 桁までの数字。無ければ空</returns>
    private static string FormatHours(double? hours) => hours?.ToString("0.##") ?? "";

    /// <summary>文章の先頭の 1 行を返す</summary>
    /// <param name="text">文章 (改行を含んでよい)</param>
    /// <returns>先頭の 1 行。2 行以上あるときは、省略を示す記号を足す</returns>
    private static string FirstLine(string text)
    {
        var index = text.IndexOfAny(['\r', '\n']);
        return index < 0 ? text : text[..index] + " …";
    }
}
