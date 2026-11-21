using System.Globalization;
using MmmTool.Notes.Core;

namespace MmmTool.Data.Notes;

/// <summary>NoteAttribute の種類の番号</summary>
/// <remarks>
/// 種類を足すときは最後の番号の次を足す
/// 一度使った番号は同じ意味のまま変えない
/// </remarks>
public static class NoteAttributeKind
{
    /// <summary>日程</summary>
    /// <remarks>
    /// 日付だけの枝番は、ほかの実施日と同じ。
    /// 項目は <see cref="NoteAttributeItem"/> の日付・開始・終了・内容・備考。
    /// </remarks>
    public const int Schedule = 1;

    /// <summary>このノートが使う部品</summary>
    /// <remarks>
    /// フラグで、値は使う部品の種類の番号。
    /// 日程なら <see cref="Schedule"/> の値の 1。
    /// 行が無ければ通常のノート。
    /// </remarks>
    public const int UsedPart = 2;
}

/// <summary>NoteAttribute の項目の番号</summary>
/// <remarks>
/// 項目の番号は種類ごとに決まる。
/// 項目を足すとき、その種類の最後の番号の次を足す。
/// 一度使った番号は同じ意味のまま変えない。
/// </remarks>
public static class NoteAttributeItem
{
    /// <summary>日程の日付</summary>
    /// <remarks>値は yyyy-MM-dd の日付</remarks>
    public const int Date = 1;

    /// <summary>日程の開始の時刻</summary>
    /// <remarks>値は HH:mm</remarks>
    public const int Start = 2;

    /// <summary>日程の終了の時刻</summary>
    /// <remarks>値は HH:mm</remarks>
    public const int End = 3;

    /// <summary>日程の内容</summary>
    public const int Content = 4;

    /// <summary>日程の備考</summary>
    public const int Remark = 5;

    /// <summary>使う部品</summary>
    /// <remarks>種類 <see cref="NoteAttributeKind.UsedPart"/> の項目</remarks>
    public const int Part = 1;
}

/// <summary>NoteAttribute の 1 行の位置</summary>
/// <param name="CategoryNo">種類の番号</param>
/// <param name="BranchNo">枝番で、種類の中の 1 件。</param>
/// <param name="CodeNo">項目の番号</param>
public readonly record struct NoteAttributeKey(int CategoryNo, int BranchNo, int CodeNo);

/// <summary>NoteAttribute の保存済みの 1 行</summary>
/// <param name="Key">行の位置</param>
/// <param name="Value">値</param>
/// <param name="IsDeleted">論理削除されているか</param>
public sealed record NoteAttributeRow(NoteAttributeKey Key, string Value, bool IsDeleted);

/// <summary>NoteAttribute に書きたい 1 行</summary>
/// <param name="Key">行の位置</param>
/// <param name="Value">値</param>
public sealed record NoteAttributeValue(NoteAttributeKey Key, string Value);

/// <summary>保存済みの行を書きたい内容に合わせるために行へ行う操作</summary>
/// <param name="Update">値を書き直して削除を戻す行</param>
/// <param name="Insert">足す行</param>
/// <param name="Delete">論理削除する行の位置</param>
public sealed record NoteAttributeChanges(IReadOnlyList<NoteAttributeValue> Update, IReadOnlyList<NoteAttributeValue> Insert, IReadOnlyList<NoteAttributeKey> Delete)
{
    /// <summary>変える行が 1 つも無いか</summary>
    public bool IsEmpty => Update.Count == 0 && Insert.Count == 0 && Delete.Count == 0;
}

/// <summary>1 つのノートの NoteAttribute から読んだ内容</summary>
/// <param name="Kind">ノートの種類</param>
/// <param name="Items">日程の行で、日付だけの行を含む。</param>
public sealed record NoteAttributeData(NoteKind Kind, IReadOnlyList<ScheduleItem> Items);

/// <summary>NoteAttribute の行と、ノートの種類・日程を相互に変える共通の処理</summary>
/// <remarks>SQLite と SQL Server の保存先が同じものを使う</remarks>
public static class NoteAttributes
{
    /// <summary>日付を保存する値の文字にする</summary>
    /// <param name="day">日付</param>
    /// <returns><c>yyyy-MM-dd</c> の文字</returns>
    public static string ToValue(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>時刻を保存する値の文字にする</summary>
    /// <param name="time">時刻</param>
    /// <returns><c>HH:mm</c> の文字</returns>
    public static string ToValue(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>書きたい行の一覧を作る</summary>
    /// <param name="kind">ノートの種類</param>
    /// <param name="items">日程の行で、種類が通常なら日付だけの行 (ほかの実施日)。</param>
    /// <returns>
    /// 種類が通常以外なら、使う部品の行を 1 行持つ。
    /// 日程の行は、渡した順に枝番 1 から振り、日付の行は必ず書く。
    /// 時刻と内容と備考は、値があるときだけ書く。
    /// </returns>
    public static IReadOnlyList<NoteAttributeValue> Desired(NoteKind kind, IReadOnlyList<ScheduleItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var rows = new List<NoteAttributeValue>();
        if (kind != NoteKind.Normal)
        {
            rows.Add(new NoteAttributeValue(new NoteAttributeKey(NoteAttributeKind.UsedPart, 1, NoteAttributeItem.Part), ((int)kind).ToString(CultureInfo.InvariantCulture)));
        }

        for (var index = 0; index < items.Count; index++)
        {
            var branch = index + 1;
            var item = items[index];
            rows.Add(Row(branch, NoteAttributeItem.Date, ToValue(item.Date)));
            if (item.Start is { } start)
            {
                rows.Add(Row(branch, NoteAttributeItem.Start, ToValue(start)));
            }
            if (item.End is { } end)
            {
                rows.Add(Row(branch, NoteAttributeItem.End, ToValue(end)));
            }
            if (item.Content.Length > 0)
            {
                rows.Add(Row(branch, NoteAttributeItem.Content, item.Content));
            }
            if (item.Remark.Length > 0)
            {
                rows.Add(Row(branch, NoteAttributeItem.Remark, item.Remark));
            }
        }
        return rows;
    }

    /// <summary>保存済みの行を書きたい行に合わせるための操作を計算する</summary>
    /// <param name="existing">そのノートの保存済みの行 (削除済みを含む)</param>
    /// <param name="desired">書きたい行</param>
    /// <returns>
    /// 行があって値か削除の状態が合わないものは書き直して削除を戻し、行が無いものは足す。
    /// 書きたい行に無くて、削除されていない行は論理削除する。
    /// 変わらない行は触らない。
    /// </returns>
    /// <remarks>
    /// 削除済みの行を使い回すので、行の数はそのノートで同時に持った最大の数で止まる。
    /// 枝番は他の表から指されず、並びの位置を表すだけなので、行の値を書き直して使い回しても困らない。
    /// </remarks>
    public static NoteAttributeChanges Diff(IReadOnlyList<NoteAttributeRow> existing, IReadOnlyList<NoteAttributeValue> desired)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(desired);

        var existingByKey = existing.ToDictionary(row => row.Key);
        var desiredKeys = desired.Select(row => row.Key).ToHashSet();
        var update = new List<NoteAttributeValue>();
        var insert = new List<NoteAttributeValue>();
        foreach (var row in desired)
        {
            if (!existingByKey.TryGetValue(row.Key, out var saved))
            {
                insert.Add(row);
            }
            else if (saved.IsDeleted || saved.Value != row.Value)
            {
                update.Add(row);
            }
        }

        var delete = existing.Where(row => !row.IsDeleted && !desiredKeys.Contains(row.Key)).Select(row => row.Key).ToList();
        return new NoteAttributeChanges(update, insert, delete);
    }

    /// <summary>1 つのノートの削除されていない行から、種類と日程を読む</summary>
    /// <param name="rows">そのノートの削除されていない行で、種類 1 と 2 のもの。</param>
    /// <returns>
    /// 種類と日程の行で、日付と開始の順に並び、同じなら枝番の順。
    /// 日付の行が読めない枝番は、無いものとして飛ばす。
    /// 時刻に直せない値は、無いものとして扱う。
    /// </returns>
    public static NoteAttributeData Read(IEnumerable<(NoteAttributeKey Key, string Value)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var list = rows.ToList();

        var kind = NoteKind.Normal;
        var flag = list.FirstOrDefault(row => row.Key.CategoryNo == NoteAttributeKind.UsedPart && row.Key.CodeNo == NoteAttributeItem.Part);
        if (flag.Value is not null && int.TryParse(flag.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && Enum.IsDefined((NoteKind)number))
        {
            kind = (NoteKind)number;
        }

        var items = new List<ScheduleItem>();
        foreach (var branch in list.Where(row => row.Key.CategoryNo == NoteAttributeKind.Schedule).GroupBy(row => row.Key.BranchNo).OrderBy(group => group.Key))
        {
            string? Find(int code) => branch.FirstOrDefault(row => row.Key.CodeNo == code).Value;
            if (!DateOnly.TryParseExact(Find(NoteAttributeItem.Date), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }
            items.Add(new ScheduleItem(date, ParseTime(Find(NoteAttributeItem.Start)), ParseTime(Find(NoteAttributeItem.End)), Find(NoteAttributeItem.Content) ?? "", Find(NoteAttributeItem.Remark) ?? ""));
        }
        return new NoteAttributeData(kind, ScheduleItem.Sort(items));
    }

    /// <summary>行を作る</summary>
    /// <param name="branch">枝番</param>
    /// <param name="code">項目の番号</param>
    /// <param name="value">値</param>
    /// <returns>日程の行</returns>
    private static NoteAttributeValue Row(int branch, int code, string value)
        => new(new NoteAttributeKey(NoteAttributeKind.Schedule, branch, code), value);

    /// <summary>時刻の文字を時刻にする</summary>
    /// <param name="value">HH:mm の文字で、無ければ null。</param>
    /// <returns>時刻で、直せなければ null。</returns>
    private static TimeOnly? ParseTime(string? value)
        => value is not null && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
}
