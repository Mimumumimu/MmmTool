using System.Globalization;

namespace MmmTool.Data.Notes;

/// <summary>
/// <c>NoteAttribute</c> の種類の番号。
/// </summary>
/// <remarks>種類を足すときは、最後の番号の次を足す。一度使った番号は、同じ意味のまま変えない。</remarks>
public static class NoteAttributeKind
{
    /// <summary>ほかの実施日。値は <c>yyyy-MM-dd</c> の日付</summary>
    public const int OtherEventDate = 1;
}

/// <summary>
/// <c>NoteAttribute</c> の 1 行で、ほかの実施日の保存済みの行。
/// </summary>
/// <param name="CodeNo">枝番</param>
/// <param name="Value">値 (<c>yyyy-MM-dd</c> の日付の文字)</param>
/// <param name="IsDeleted">論理削除されているか</param>
public sealed record NoteAttributeRow(int CodeNo, string Value, bool IsDeleted);

/// <summary>
/// ほかの実施日の保存の差で、新しい日付の一覧に合わせるために、行へ行う操作。
/// </summary>
/// <param name="Update">ある行の日付を書き直し、削除を戻す行の枝番と日付</param>
/// <param name="Insert">足す行の枝番と日付</param>
/// <param name="Delete">論理削除する行の枝番</param>
public sealed record NoteOtherEventDateChanges(IReadOnlyList<(int CodeNo, DateOnly Day)> Update, IReadOnlyList<(int CodeNo, DateOnly Day)> Insert, IReadOnlyList<int> Delete)
{
    /// <summary>変える行が 1 つも無いか</summary>
    public bool IsEmpty => Update.Count == 0 && Insert.Count == 0 && Delete.Count == 0;
}

/// <summary>
/// ほかの実施日を、<c>NoteAttribute</c> の行と相互に変える共通の処理。SQLite と SQL Server の保存先が、同じものを使う。
/// </summary>
public static class NoteOtherEventDates
{
    /// <summary>日付を、保存する値の文字にする</summary>
    /// <param name="day">日付</param>
    /// <returns><c>yyyy-MM-dd</c> の文字</returns>
    public static string ToValue(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>保存された値の文字から、日付の一覧にする</summary>
    /// <param name="values">削除されていない行の値</param>
    /// <returns>日付の順で、同じ日を重ねない一覧。日付に直せない値は、無いものとして飛ばす</returns>
    public static IReadOnlyList<DateOnly> ToDates(IEnumerable<string> values)
    {
        var dates = new SortedSet<DateOnly>();
        foreach (var value in values)
        {
            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                dates.Add(day);
            }
        }
        return [.. dates];
    }

    /// <summary>保存済みの行を、新しい日付の一覧に合わせるための操作を計算する</summary>
    /// <param name="existing">そのノートの、保存済みの行 (削除済みを含む)</param>
    /// <param name="desired">保存したい日付の一覧</param>
    /// <returns>
    /// 日付を日付の順に並べて、1 番目を枝番 1、2 番目を枝番 2 …の行に書く。枝番が日付の順になり、散らばらない。
    /// 行があって、日付と削除の状態が合わないものは、<see cref="NoteOtherEventDateChanges.Update"/> で書き直して削除を戻し、行が無い枝番は足す。
    /// 日付の数より後ろの枝番で、削除されていない行は、論理削除する。変わらない行は触らない。
    /// </returns>
    /// <remarks>
    /// 削除済みの行を使い回すので、行の数は、そのノートで同時に持った日付の最大の数で止まる。
    /// 枝番は、他の表から指されず、日付の順の位置を表すだけなので、行の日付を書き直して使い回しても困らない。
    /// </remarks>
    public static NoteOtherEventDateChanges Diff(IReadOnlyList<NoteAttributeRow> existing, IReadOnlyList<DateOnly> desired)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(desired);

        var ordered = desired.Distinct().Order().ToList();
        var byCodeNo = existing.ToDictionary(row => row.CodeNo);
        var update = new List<(int CodeNo, DateOnly Day)>();
        var insert = new List<(int CodeNo, DateOnly Day)>();
        for (var index = 0; index < ordered.Count; index++)
        {
            var codeNo = index + 1;
            var day = ordered[index];
            if (!byCodeNo.TryGetValue(codeNo, out var row))
            {
                insert.Add((codeNo, day));
            }
            else if (row.IsDeleted || row.Value != ToValue(day))
            {
                update.Add((codeNo, day));
            }
        }

        var delete = existing.Where(row => row.CodeNo > ordered.Count && !row.IsDeleted).Select(row => row.CodeNo).ToList();
        return new NoteOtherEventDateChanges(update, insert, delete);
    }
}
