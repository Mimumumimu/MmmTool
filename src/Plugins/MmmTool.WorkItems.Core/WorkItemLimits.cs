namespace MmmTool.WorkItems.Core;

/// <summary>入力の上限 (DB の列の長さと同じ。画面の入力欄にも使う)</summary>
public static class WorkItemLimits
{
    /// <summary>名前の最大の長さ</summary>
    public const int NameMaxLength = 200;

    /// <summary>備考 (作業の共通の備考・日の備考)の最大の長さ</summary>
    public const int NoteMaxLength = 1000;

    /// <summary>予定時間の最大</summary>
    public const double PlannedHoursMax = 9999;

    /// <summary>1 日の実績の最大 (時間)</summary>
    public const double DailyHoursMax = 24;
}
