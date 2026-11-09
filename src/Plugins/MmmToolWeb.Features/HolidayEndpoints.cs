using MmmSdk.Core.Components.Holidays;

namespace MmmToolWeb.Features;

/// <summary>
/// ある年の、国民の祝日・休日の名前と日付。
/// </summary>
/// <param name="Date">日付で、yyyy-MM-dd の形。</param>
/// <param name="Name">祝日・休日の名前。</param>
public sealed record HolidayResponse(string Date, string Name);

/// <summary>
/// 日付を選ぶカレンダーが、祝日を出すための API。
/// </summary>
/// <remarks>
/// 祝日の一覧 (内閣府の <c>syukujitsu.csv</c>)は、サーバーの <c>Assets/Holidays</c> にあり、ブラウザの中では読めない。
/// ブラウザにも同じ一覧を持たせると、一覧を置き換えるときに 2 か所を直すことになるため、サーバーが年ごとに返す。
/// </remarks>
public static class HolidayEndpoints
{
    /// <summary>西暦の年として扱える最小の年。</summary>
    private const int MinYear = 1;

    /// <summary>西暦の年として扱える最大の年。</summary>
    private const int MaxYear = 9999;

    /// <summary>
    /// 祝日の API を登録する。
    /// </summary>
    /// <param name="api">ログイン必須の <c>/api</c>。</param>
    /// <returns>続けて呼ぶための <paramref name="api"/>。</returns>
    public static RouteGroupBuilder MapHolidays(this RouteGroupBuilder api)
    {
        api.MapGet("/holidays", GetYear);
        return api;
    }

    /// <summary>
    /// ある年の祝日を、日付の順に返す。
    /// </summary>
    /// <param name="year">西暦の年。</param>
    /// <returns>祝日の一覧。一覧に無い年は空。年が範囲の外のときは 400。</returns>
    private static IResult GetYear(int year)
        => year is < MinYear or > MaxYear
            ? Results.BadRequest()
            : Results.Ok(JapaneseHolidays.GetHolidays(year).Select(holiday => new HolidayResponse(holiday.Key.ToString("yyyy-MM-dd"), holiday.Value)));
}
