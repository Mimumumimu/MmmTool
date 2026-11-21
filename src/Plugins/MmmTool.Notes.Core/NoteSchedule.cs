namespace MmmTool.Notes.Core;

/// <summary>ノートの種類</summary>
/// <remarks>
/// 作ったあとに変えない
/// 通常以外の値は保存先の <c>NoteAttribute</c> の種類の番号と同じ
/// 部品を足すとき最後の番号の次を足す
/// 一度使った番号は同じ意味のまま変えない
/// </remarks>
public enum NoteKind
{
    /// <summary>通常のノート</summary>
    Normal = 0,

    /// <summary>日程を持つノート</summary>
    Schedule = 1,
}

/// <summary>日程の 1 件</summary>
/// <param name="Date">日付</param>
/// <param name="Start">開始の時刻で、無ければ null。</param>
/// <param name="End">終了の時刻で、無ければ null。</param>
/// <param name="Content">内容</param>
/// <param name="Remark">備考</param>
/// <remarks>
/// 同じ日に何件でも持てる
/// 日付だけの 1 件はほかの実施日と同じ
/// </remarks>
public sealed record ScheduleItem(DateOnly Date, TimeOnly? Start, TimeOnly? End, string Content, string Remark)
{
    /// <summary>日付と開始の順に並べる</summary>
    /// <param name="items">並べる日程</param>
    /// <returns>
    /// 日付の順で、同じ日は開始の時刻の順に並べた一覧。
    /// 開始が無いものが先で、同じなら渡した順。
    /// </returns>
    public static IReadOnlyList<ScheduleItem> Sort(IEnumerable<ScheduleItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return [.. items.OrderBy(item => item.Date).ThenBy(item => item.Start ?? TimeOnly.MinValue).ThenBy(item => item.Start.HasValue ? 1 : 0)];
    }

    /// <summary>日付だけの日程にする</summary>
    /// <param name="date">日付</param>
    /// <returns>時刻・内容・備考が空の日程</returns>
    public static ScheduleItem OfDate(DateOnly date) => new(date, null, null, "", "");
}
