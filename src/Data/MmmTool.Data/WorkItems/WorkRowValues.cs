using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>作業管理の表の行と、アプリの型の間の、値の変換と検査 (表ごとの行の型が共有する)</summary>
/// <remarks>
/// 「ない」は、日付が <c>9999-12-31</c> (開始日だけ <c>0001-01-01</c>。「いつからでも」を最小値で表す)、予定時間が 0、文字列が空文字 (NULL は使わない。理由は docs/specs/database.md)。
/// DB に入れられない値 (長さ・範囲の外。手で直したデータなど)は、バグなので <see cref="ArgumentException"/> にする (握りつぶさない・黙って切り詰めない)。
/// </remarks>
public static class WorkRowValues
{
    /// <summary>日付の「ない」を表す値 (期限日・完了日)</summary>
    public static readonly DateOnly NoDate = DateOnly.MaxValue;

    /// <summary>開始日の「ない」を表す値 (いつからでも始められる)</summary>
    public static readonly DateOnly NoStartDate = DateOnly.MinValue;

    /// <summary>アプリの日付 (無ければ null)を、行の日付にする</summary>
    /// <param name="date">日付。無ければ null</param>
    /// <param name="name">項目の名前 (エラーのメッセージに使う)</param>
    /// <param name="none">「ない」を表す値 (既定は <see cref="NoDate"/>。開始日は <see cref="NoStartDate"/>)</param>
    /// <returns>行の日付 (ない日は <paramref name="none"/>)</returns>
    /// <exception cref="ArgumentException"><paramref name="none"/> は「ない」を表すので、日付には使えない。</exception>
    public static DateOnly FromDate(DateOnly? date, string name, DateOnly? none = null)
    {
        var noneValue = none ?? NoDate;
        if (date is null)
        {
            return noneValue;
        }
        return date.Value == noneValue
            ? throw new ArgumentException($"{name}に {noneValue:yyyy-MM-dd} は使えません (日付なしを表す値のため)。", nameof(date))
            : date.Value;
    }

    /// <summary>行の日付を、アプリの日付にする</summary>
    /// <param name="date">行の日付</param>
    /// <param name="none">「ない」を表す値 (既定は <see cref="NoDate"/>。開始日は <see cref="NoStartDate"/>)</param>
    /// <returns>日付。ない日 (<paramref name="none"/>)は null</returns>
    public static DateOnly? ToDate(DateOnly date, DateOnly? none = null) => date == (none ?? NoDate) ? null : date;

    /// <summary>アプリの時間 (小数 2 桁)を、行の時間にする</summary>
    /// <param name="hours">時間</param>
    /// <param name="name">項目の名前 (エラーのメッセージに使う)</param>
    /// <param name="max">最大</param>
    /// <returns>小数 2 桁に丸めた時間</returns>
    /// <exception cref="ArgumentException">0 未満・最大を超える・数でない。</exception>
    public static decimal FromHours(double hours, string name, double max)
    {
        if (double.IsNaN(hours) || hours < 0 || hours > max)
        {
            throw new ArgumentException($"{name}は 0 以上 {max} 以下にしてください (指定: {hours})。", nameof(hours));
        }
        return Math.Round((decimal)hours, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>文字列の長さを検査する</summary>
    /// <param name="value">文字列</param>
    /// <param name="name">項目の名前 (エラーのメッセージに使う)</param>
    /// <param name="maxLength">最大の長さ</param>
    /// <param name="required">空を許さないか</param>
    /// <returns>同じ文字列</returns>
    /// <exception cref="ArgumentException">長すぎる・必須なのに空。</exception>
    public static string CheckText(string value, string name, int maxLength, bool required = false)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{name}は {maxLength} 文字までです (指定: {value.Length} 文字)。", nameof(value));
        }
        return required && value.Length == 0 ? throw new ArgumentException($"{name}が空です。", nameof(value)) : value;
    }

    /// <summary>行の時間を、アプリの時間にする</summary>
    /// <param name="hours">行の時間</param>
    /// <returns>時間</returns>
    public static double ToHours(decimal hours) => (double)hours;

    /// <summary>名前の長さの上限 (<see cref="WorkItemLimits.NameMaxLength"/>)</summary>
    public const int NameMax = WorkItemLimits.NameMaxLength;

    /// <summary>備考の長さの上限 (<see cref="WorkItemLimits.NoteMaxLength"/>)</summary>
    public const int NoteMax = WorkItemLimits.NoteMaxLength;
}
