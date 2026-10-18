namespace MmmToolWeb.Features;

/// <summary>
/// 機能 (<see cref="IWebFeature"/>)を、サーバーに足す・使う。
/// </summary>
public static class WebFeatureExtensions
{
    /// <summary>
    /// 機能を 1 つ足す。機能を足すときに、サーバー本体で書くのは、この 1 行だけ。
    /// </summary>
    /// <typeparam name="TFeature">足す機能。</typeparam>
    /// <param name="services">登録先。</param>
    /// <returns>続けて呼ぶための <paramref name="services"/>。</returns>
    public static IServiceCollection AddWebFeature<TFeature>(this IServiceCollection services)
        where TFeature : IWebFeature, new()
    {
        var feature = new TFeature();
        services.AddSingleton<IWebFeature>(feature);
        feature.AddServices(services);
        return services;
    }

    /// <summary>
    /// 足した機能の API を、すべて登録する。
    /// </summary>
    /// <param name="app">登録先。</param>
    /// <returns>続けて呼ぶための <paramref name="app"/>。</returns>
    /// <remarks>機能の API は、<c>/api</c> の下にまとめ、ログインしている人だけが呼べるようにする。機能ごとに、付け忘れないため。</remarks>
    public static IEndpointRouteBuilder MapWebFeatures(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        foreach (var feature in app.ServiceProvider.GetServices<IWebFeature>())
        {
            feature.MapEndpoints(api);
        }
        return app;
    }
}
