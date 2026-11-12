namespace MmmTool.WorkItems.Core;

/// <summary>入力の上限で、DB の列の長さと同じ</summary>
/// <remarks>画面の入力欄にも使う</remarks>
public static class WorkItemLimits
{
    /// <summary>名前の最大の長さ</summary>
    public const int NameMaxLength = 200;

    /// <summary>備考の最大の長さ</summary>
    public const int NoteMaxLength = 1000;

    /// <summary>日報メモの最大の長さ</summary>
    public const int DayNoteMaxLength = 2000;

    /// <summary>リンクの最大の長さ</summary>
    public const int LinkMaxLength = 2000;

    /// <summary>予定時間の最大</summary>
    public const double PlannedHoursMax = 9999;

    /// <summary>1 日の実績の最大</summary>
    public const double DailyHoursMax = 24;
}
