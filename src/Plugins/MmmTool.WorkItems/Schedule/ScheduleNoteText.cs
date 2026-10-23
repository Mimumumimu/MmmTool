namespace MmmTool.WorkItems.Schedule;

/// <summary>スケジュールの吹き出しに出す、備考の文を整える</summary>
public static class ScheduleNoteText
{
    /// <summary>吹き出しに出す、備考の文字数の上限 (作業管理の表のセルと同じ)</summary>
    public const int MaxLength = 320;

    /// <summary>上限を超える備考を、「…」で切る</summary>
    /// <param name="note">備考</param>
    /// <returns>上限以内ならそのまま、超えるなら上限までに「…」を付けた文</returns>
    /// <remarks>全文は、作業管理の表のセルで読める。</remarks>
    public static string Shorten(string note)
        => note.Length <= MaxLength ? note : note[..MaxLength] + "…";
}
