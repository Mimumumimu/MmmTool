namespace MmmTool.Notes.Core;

/// <summary>実施日とほかの実施日を合わせて扱う計算</summary>
/// <remarks>PC 版と Web 版が同じものを使う</remarks>
public static class NoteEventDates
{
    /// <summary>指定の日より後で、いちばん近い実施日を取得する</summary>
    /// <param name="eventDate">
    /// 実施日
    /// 未設定は null か <see cref="DateOnly.MaxValue"/>
    /// </param>
    /// <param name="otherEventDates">ほかの実施日</param>
    /// <param name="day">基準の日</param>
    /// <returns>
    /// 基準の日より後で、いちばん近い日。
    /// 無ければ null。
    /// </returns>
    public static DateOnly? NextAfter(DateOnly? eventDate, IReadOnlyList<DateOnly> otherEventDates, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(otherEventDates);

        DateOnly? next = eventDate is { } main && main != DateOnly.MaxValue && main > day ? main : null;
        foreach (var other in otherEventDates)
        {
            if (other > day && (next is null || other < next))
            {
                next = other;
            }
        }
        return next;
    }
}
