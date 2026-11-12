using System.Globalization;

namespace MmmToolWeb.Features.Client;

/// <summary>日付を API の要求や通知から開く道に載せる文字との間で変える</summary>
/// <remarks>
/// 書く側 (画面の API 呼び出しと、サーバーの通知の宛先)と、読む側 (画面)で、同じ形を使うため、ここに 1 つだけ置く。
/// 端末の文化 (言語・暦)に左右されないよう、常に固定の文化 (<see cref="CultureInfo.InvariantCulture"/>)で変える。
/// </remarks>
public static class ApiDay
{
    /// <summary>日付の書式</summary>
    public const string Format = "yyyy-MM-dd";

    /// <summary>日付を要求や道に載せる文字にする</summary>
    /// <param name="day">日</param>
    /// <returns><see cref="Format"/> の文字</returns>
    public static string ToText(DateOnly day) => day.ToString(Format, CultureInfo.InvariantCulture);

    /// <summary>要求や道に載っていた文字を日付にする</summary>
    /// <param name="text">
    /// 文字
    /// 無いときは null
    /// </param>
    /// <param name="day">
    /// 日。
    /// 読めたときだけ、中身が入る。
    /// </param>
    /// <returns><see cref="Format"/> の形で読めたとき true</returns>
    public static bool TryParse(string? text, out DateOnly day)
        => DateOnly.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
}
