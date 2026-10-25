using Microsoft.Extensions.DependencyInjection;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// 機能の画面が共通で使う部品と、機能の登録。
/// </summary>
public static class ClientFeatureServiceCollectionExtensions
{
    /// <summary>
    /// 機能の画面が共通で使う部品 (<see cref="BrowserClient"/>・<see cref="SessionClient"/>・<see cref="FeatureAvailability"/>)を登録する。
    /// </summary>
    /// <param name="services">登録先。</param>
    /// <returns>続けて呼ぶための <paramref name="services"/>。</returns>
    public static IServiceCollection AddClientFeatureSupport(this IServiceCollection services)
    {
        services.AddScoped<BrowserClient>();
        services.AddScoped<SessionClient>();
        services.AddScoped<FeatureAvailability>();
        return services;
    }

    /// <summary>
    /// 機能を 1 つ足す。
    /// </summary>
    /// <param name="services">登録先。</param>
    /// <param name="feature">足す機能。</param>
    /// <returns>続けて呼ぶための <paramref name="services"/>。</returns>
    public static IServiceCollection AddClientFeature(this IServiceCollection services, ClientFeature feature)
        => services.AddSingleton(feature);
}
