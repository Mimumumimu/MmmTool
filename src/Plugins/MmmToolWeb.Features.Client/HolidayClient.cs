using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// サーバーが返す、ある年の祝日 1 件。
/// </summary>
/// <param name="Date">日付で、yyyy-MM-dd の形。</param>
/// <param name="Name">祝日・休日の名前。</param>
internal sealed record HolidayItem(string Date, string Name);

/// <summary>
/// 祝日の API の JSON を読む、ソース生成の設定。
/// </summary>
/// <remarks>ブラウザの発行では、使っていない型が削られるので、型を書いて調べる方式 (リフレクション)に頼らない。</remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HolidayItem[]))]
internal sealed partial class HolidayJsonContext : JsonSerializerContext;

/// <summary>
/// 日付を選ぶカレンダー (<see cref="DatePicker"/>)に出す、祝日をサーバーから読む。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
/// <remarks>
/// 祝日の一覧はサーバーにだけある。年ごとに 1 回読み、読めた年は覚えておく。
/// 同じ年を同時に何か所からも頼まれたときは、1 回の問い合わせを分け合う。日付の欄が 1 つの画面に何個もあるため。
/// 読めなかったときは、祝日なしとして扱い、次に頼まれたときにもう一度読む。祝日が出なくても、日付は選べるようにするため。
/// </remarks>
public sealed class HolidayClient(HttpClient http)
{
    /// <summary>祝日が無いことを表す、空の一覧。</summary>
    private static readonly IReadOnlyDictionary<DateOnly, string> None = new Dictionary<DateOnly, string>();

    /// <summary>年ごとの、読み込み中または読めた祝日。</summary>
    private readonly Dictionary<int, Task<IReadOnlyDictionary<DateOnly, string>>> _years = [];

    /// <summary>
    /// ある年の祝日を返す。
    /// </summary>
    /// <param name="year">西暦の年。</param>
    /// <returns>日付と名前。祝日の一覧に無い年と、読めなかったときは空。</returns>
    public Task<IReadOnlyDictionary<DateOnly, string>> GetYearAsync(int year)
    {
        if (!_years.TryGetValue(year, out var task))
        {
            task = LoadAsync(year);
            _years[year] = task;
        }
        return task;
    }

    /// <summary>
    /// サーバーから、ある年の祝日を読む。
    /// </summary>
    /// <param name="year">西暦の年。</param>
    /// <returns>日付と名前。読めなかったときは空で、次に頼まれたときに読み直す。</returns>
    private async Task<IReadOnlyDictionary<DateOnly, string>> LoadAsync(int year)
    {
        try
        {
            var items = await http.GetFromJsonAsync($"api/holidays?year={year}", HolidayJsonContext.Default.HolidayItemArray);
            var holidays = new Dictionary<DateOnly, string>();
            foreach (var item in items ?? [])
            {
                if (DateOnly.TryParseExact(item.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    holidays[date] = item.Name;
                }
            }
            return holidays;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // 時間切れも、通信の失敗と同じに扱う。失敗した問い合わせを覚えたままにすると、次からも失敗し続けるため
            _years.Remove(year);
            return None;
        }
    }
}
