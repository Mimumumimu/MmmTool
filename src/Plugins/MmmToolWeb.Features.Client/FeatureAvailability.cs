using System.Net.Http.Json;
using System.Text.Json;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// ログインしている人が、管理者に使えなくされた機能を、サーバーに聞いて、画面から外す。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
/// <remarks>
/// サーバーは、使えない機能の API を断る。この部品は、ホーム画面と設定の画面に、使えない機能を出さないためだけに使う。
/// 機能は、画面側のアセンブリの名前で区別する。サーバーの機能の登録と、画面の機能の登録が、同じアセンブリを指すため。
/// 前に聞いた結果を覚えていて、画面を開いた直後は、それを先に出す。開くたびに空の画面を挟まないため。聞き直した結果で、すぐ入れ替える。
/// 聞けなかったときは、前の結果のまま。1 度も聞けていなければ、外さない。使えない機能の API は、サーバーが断る。
/// </remarks>
public sealed class FeatureAvailability(HttpClient http)
{
    /// <summary>前に聞けた、使えない機能の画面側のアセンブリの名前。まだ聞けていなければ null。</summary>
    private HashSet<string>? _lastDisabled;

    /// <summary>
    /// 前に聞いた結果で、使える機能だけを残す。
    /// </summary>
    /// <param name="features">画面に足した機能。</param>
    /// <returns>使える機能。まだ 1 度も聞けていなければ null。</returns>
    public IReadOnlyList<ClientFeature>? FilterRemembered(IEnumerable<ClientFeature> features)
        => _lastDisabled is null ? null : Apply(features, _lastDisabled);

    /// <summary>
    /// サーバーに聞いて、使える機能だけを残す。
    /// </summary>
    /// <param name="features">画面に足した機能。</param>
    /// <returns>使える機能。順序は、渡した順のまま。</returns>
    public async Task<IReadOnlyList<ClientFeature>> FilterAsync(IEnumerable<ClientFeature> features)
    {
        if (await GetDisabledAsync() is { } disabled)
        {
            _lastDisabled = disabled;
        }

        return Apply(features, _lastDisabled ?? []);
    }

    /// <summary>
    /// 使えない機能を除く。
    /// </summary>
    /// <param name="features">画面に足した機能。</param>
    /// <param name="disabled">使えない機能の、画面側のアセンブリの名前。</param>
    /// <returns>使える機能。</returns>
    private static IReadOnlyList<ClientFeature> Apply(IEnumerable<ClientFeature> features, HashSet<string> disabled)
        => [.. features.Where(feature => !disabled.Contains(feature.Assembly.GetName().Name ?? ""))];

    /// <summary>
    /// 使えない機能の、画面側のアセンブリの名前を、サーバーから取得する。
    /// </summary>
    /// <returns>アセンブリの名前。聞けなかったときは null。</returns>
    private async Task<HashSet<string>?> GetDisabledAsync()
    {
        try
        {
            var names = await http.GetFromJsonAsync("api/features/disabled", FeatureAvailabilityJsonContext.Default.StringArray);
            return [.. names ?? []];
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }
}
