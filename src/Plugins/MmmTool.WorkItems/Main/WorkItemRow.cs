using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.WinUI.Controls;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Main;

/// <summary>表の 1 行 (案件・グループ・作業のどれかを、表の列に出す形にしたもの)</summary>
/// <param name="key">行の識別 (種類 + 番号。作ったときに決まり、変わらない)</param>
/// <remarks>
/// 表に出している間は同じ行を使い回し、内容が変わったときは <see cref="Update"/> で値だけを書き換える (入力欄の位置・フォーカスを保つため)。
/// 案件・グループの日付・予定時間・実績・進捗度は、下の作業から計算した値の表示 (入力はできない)。
/// </remarks>
public sealed partial class WorkItemRow(WorkKey key) : ObservableObject, ITreeGridRow
{
    /// <summary>行の識別 (種類 + 番号)</summary>
    public WorkKey Key { get; } = key;

    /// <summary>行の番号 (種類ごとに別の連番)</summary>
    public int Id => Key.Id;

    /// <summary>種類</summary>
    public WorkRowKind Kind => Key.Kind;

    /// <summary>集計の行か (案件・グループ。下の作業から計算した値を出す)</summary>
    public bool IsGroup { get; } = key.Kind != WorkRowKind.Work;

    /// <summary>作業の行か</summary>
    public bool IsWork { get; } = key.Kind == WorkRowKind.Work;

    /// <summary>字下げの段</summary>
    [ObservableProperty]
    public partial int Level { get; private set; }

    /// <summary>案件か</summary>
    public bool IsProject { get; } = key.Kind == WorkRowKind.Project;

    /// <summary>行に重ねる色 (案件は青の半透明。触れない作業は灰色の半透明。ほかは重ねない)</summary>
    /// <remarks>灰色は、中間の灰色 (#808080)の約 22%。ライトでは白い背景が少し暗く、ダークでは暗い背景が少し明るくなり、どちらも背景に対して約 1.3 倍の差で、終わった行と分かる。</remarks>
    public Windows.UI.Color? RowTint => IsProject
        ? Windows.UI.Color.FromArgb(0x2E, 0x4D, 0x8F, 0xE0)
        : IsLocked ? Windows.UI.Color.FromArgb(0x38, 0x80, 0x80, 0x80) : null;

    /// <summary>案件の中のグループか</summary>
    public bool IsSubGroup { get; } = key.Kind == WorkRowKind.Group;

    /// <summary>完了して日がたった作業で、触れないか (作業のみ。完了日が入力日より前)</summary>
    /// <remarks>入力日を完了日に戻すと、触れる。行の背景は灰色にする (<see cref="RowTint"/>)。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowTint))]
    [NotifyPropertyChangedFor(nameof(PlannedHintVisible))]
    [NotifyPropertyChangedFor(nameof(ActualDayHintVisible))]
    [NotifyPropertyChangedFor(nameof(CanOpenNote))]
    [NotifyPropertyChangedFor(nameof(CanOpenDayNote))]
    public partial bool IsLocked { get; private set; }

    /// <summary>備考のウィンドウを開けるか (触れない作業は、読むものがある (備考が空でない)ときだけ)</summary>
    public bool CanOpenNote => !IsLocked || Note.Length > 0;

    /// <summary>入力日の備考のウィンドウを開けるか (触れない作業は、読むものがある (備考が空でない)ときだけ)</summary>
    public bool CanOpenDayNote => !IsLocked || DayNote.Length > 0;

    /// <summary>子を持つか</summary>
    [ObservableProperty]
    public partial bool HasChildren { get; private set; }

    /// <summary>開いているか</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; private set; }

    /// <summary>名前</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>リンク (URL・フォルダ・ファイルのパス。無いときは空)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLink))]
    public partial string Link { get; private set; } = "";

    /// <summary>リンクがあるか (表のセルに「リンク」の文字を出すか)</summary>
    public bool HasLink => Link.Length > 0;

    /// <summary>備考 (全文)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteFirstLine))]
    [NotifyPropertyChangedFor(nameof(CanOpenNote))]
    public partial string Note { get; private set; } = "";

    /// <summary>備考の先頭の 1 行 (表のセルに出す)</summary>
    public string NoteFirstLine => FirstLine(Note);

    /// <summary>優先度の表示 (作業のみ)</summary>
    [ObservableProperty]
    public partial string PriorityText { get; private set; } = "";

    /// <summary>担当者 (ユーザーの番号。作業のみ。0 は未割り当て)</summary>
    [ObservableProperty]
    public partial int AssigneeUserId { get; private set; }

    /// <summary>担当者の表示名 (作業のみ。未割り当ては空)</summary>
    [ObservableProperty]
    public partial string AssigneeText { get; private set; } = "";

    /// <summary>状態の表示 (作業のみ)</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    /// <summary>進捗度の表示 (作業はコードマスタの名前つきの表記、グループは計算値)</summary>
    [ObservableProperty]
    public partial string ProgressCellText { get; private set; } = "";

    /// <summary>予定が空のとき、入力の手がかりの文字を出すか (作業のみ。触れない作業には、入力できないので出さない)</summary>
    public bool PlannedHintVisible => IsWork && !IsLocked && PlannedText.Length == 0;

    /// <summary>入力日の実績が空のとき、入力の手がかりの文字を出すか (作業のみ。触れない作業には、入力できないので出さない)</summary>
    public bool ActualDayHintVisible => IsWork && !IsLocked && ActualDayText.Length == 0;

    /// <summary>優先度の選択位置 (選択肢から外れたコードは -1)</summary>
    [ObservableProperty]
    public partial int PriorityIndex { get; private set; }

    /// <summary>状態の選択位置 (選択肢から外れたコードは -1)</summary>
    [ObservableProperty]
    public partial int StatusIndex { get; private set; }

    /// <summary>進捗度の選択位置 (選択肢から外れたコードは -1)</summary>
    [ObservableProperty]
    public partial int ProgressIndex { get; private set; }

    /// <summary>選択欄の選択肢 (コードマスタから作った一覧。行どうしで共有する)</summary>
    public WorkChoices Choices { get; private set; } = new(MmmTool.CodeMaster.Core.CodeBook.Empty);

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

    /// <summary>期限を過ぎた日数 (作業のみ。未完了で期限日が入力日より前のとき。過ぎていなければ 0)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverdue))]
    [NotifyPropertyChangedFor(nameof(IsDueNormal))]
    [NotifyPropertyChangedFor(nameof(OverdueTip))]
    public partial int OverdueDays { get; private set; }

    /// <summary>期限を過ぎているか (期限日を赤い文字と警告のアイコンで出す)</summary>
    public bool IsOverdue => IsWork && OverdueDays > 0;

    /// <summary>期限日を、通常の文字で出す作業か (期限を過ぎていない作業)</summary>
    public bool IsDueNormal => IsWork && OverdueDays == 0;

    /// <summary>期限切れのアイコンのツールチップ (「3 日超過」)</summary>
    public string OverdueTip => $"{OverdueDays} 日超過";

    /// <summary>期限日 (<c>yyyy/MM/dd</c>。無ければ空。グループは計算値)</summary>
    [ObservableProperty]
    public partial string DueText { get; private set; } = "";

    /// <summary>完了日 (<c>yyyy/MM/dd</c>。完了していなければ空。グループ・案件は、下の作業の完了日の最大値で、完了したときだけ)</summary>
    [ObservableProperty]
    public partial string CompletedText { get; private set; } = "";

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
    [NotifyPropertyChangedFor(nameof(CanOpenDayNote))]
    public partial string DayNote { get; private set; } = "";

    /// <summary>入力日の備考の先頭の 1 行 (表のセルに出す)</summary>
    public string DayNoteFirstLine => FirstLine(DayNote);

    /// <summary>行の内容を書き換える</summary>
    /// <param name="node">木構造の行 (行と集計値)</param>
    /// <param name="level">字下げの段</param>
    /// <param name="hasChildren">表に出す子を持つか (開閉ボタンを出すか)</param>
    /// <param name="isExpanded">開いているか</param>
    /// <param name="dayRecord">入力日の記録 (作業のみ。無ければ null)</param>
    /// <param name="choices">選択欄の選択肢 (優先度・状態・進捗度。グループの状態・進捗度の表示にも使う)</param>
    /// <param name="assigneeText">担当者の表示名 (作業のみ。未割り当ては空)</param>
    /// <param name="isLocked">完了して日がたった作業で、触れないか (作業のみ)</param>
    /// <param name="overdueDays">期限を過ぎた日数 (作業のみ。過ぎていなければ 0)</param>
    public void Update(
        WorkNode node,
        int level,
        bool hasChildren,
        bool isExpanded,
        WorkRecord? dayRecord,
        WorkChoices choices,
        string assigneeText,
        bool isLocked,
        int overdueDays)
    {
        var item = node.Item;
        var summary = node.Summary;

        IsLocked = isLocked;
        OverdueDays = overdueDays;
        Level = level;
        HasChildren = hasChildren;
        IsExpanded = isExpanded;
        Name = node.Name;
        Link = node.Link;
        Note = node.Note;
        Choices = choices;
        StartText = FormatDate(summary.StartDate);
        DueText = FormatDate(summary.DueDate);
        CompletedText = FormatDate(summary.CompletedDate);
        StartValue = ToValue(summary.StartDate);
        DueValue = ToValue(summary.DueDate);
        PlannedText = FormatHours(summary.PlannedHours);
        ActualTotalText = FormatHours(summary.ActualTotal > 0 ? summary.ActualTotal : null);

        if (item is not null)
        {
            AssigneeUserId = item.AssigneeUserId;
            AssigneeText = assigneeText;
            PriorityIndex = WorkChoices.IndexOf(choices.Priorities, (int)item.Priority);
            StatusIndex = WorkChoices.IndexOf(choices.Statuses, (int)item.Status);
            ProgressIndex = WorkChoices.IndexOf(choices.Progresses, item.ProgressCodeNo);
            PriorityText = WorkCodes.Display(choices.Book, WorkCodes.PriorityCategory, (int)item.Priority);
            StatusText = WorkCodes.Display(choices.Book, WorkCodes.StatusCategory, (int)item.Status);
            ProgressCellText = WorkCodes.Display(choices.Book, WorkCodes.ProgressCategory, item.ProgressCodeNo);
            ActualDayText = FormatHours(dayRecord is { Hours: > 0 } ? dayRecord.Hours : null);
            DayNote = dayRecord?.Note ?? "";
        }
        else
        {
            StatusText = summary.Status is { } status ? WorkCodes.Display(choices.Book, WorkCodes.StatusCategory, (int)status) : "";
            ProgressText = summary.Progress is { } progress ? WorkCodes.DisplayProgressPercent(choices.Book, progress) : "";
            ProgressCellText = ProgressText;
            ActualDayText = FormatHours(summary.ActualDay > 0 ? summary.ActualDay : null);
        }
    }

    /// <summary>集計の行か (案件・グループは、下の作業から計算した値だけを出す)</summary>
    public bool IsSummary => IsGroup;

    /// <summary>このセルを、入力できるか</summary>
    /// <param name="columnKey">列のキー</param>
    /// <returns>名前とリンクは、案件・グループも作業も入力できる。ほかの列は、作業だけ (グループは計算値)。触れない作業 (<see cref="IsLocked"/>)は、リンクを除いて入力できない</returns>
    /// <remarks>リンクの列は、押すとリンクを開く列なので、触れない作業でも true にする (リンクの編集は、右クリックのメニューで止める)。</remarks>
    public bool CanEdit(string columnKey) => columnKey == "Link" || (!IsLocked && (columnKey == "Name" || IsWork));

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
