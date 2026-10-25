using MmmTool.Users.Core;

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
    /// <remarks>
    /// 機能の API は、<c>/api</c> の下にまとめ、ログインしている人だけが呼べるようにする。機能ごとに、付け忘れないため。
    /// 機能番号 (<see cref="IWebFeature.FeatureNo"/>)がある機能は、管理者に無効にされた人からの呼び出しを断る。断る処理は、ここで 1 回だけ付ける。
    /// 使えない機能の一覧 <c>/api/features/disabled</c> も、ここで登録する。
    /// </remarks>
    public static IEndpointRouteBuilder MapWebFeatures(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        var features = app.ServiceProvider.GetServices<IWebFeature>().ToList();
        foreach (var feature in features)
        {
            feature.MapEndpoints(feature.FeatureNo is { } featureNo ? api.MapGroup("").AddEndpointFilter(RejectWhenDisabled(featureNo)) : api);
        }

        api.MapGet("/features/disabled", (CurrentUser currentUser, FeatureAccessService access, CancellationToken cancellationToken)
            => GetDisabledClientAssembliesAsync(features, currentUser, access, cancellationToken));
        return app;
    }

    /// <summary>
    /// 管理者に無効にされた人からの呼び出しを、404 で断る処理を作る。
    /// </summary>
    /// <param name="featureNo">機能番号。</param>
    /// <returns>API の呼び出しの前に動く処理。</returns>
    private static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> RejectWhenDisabled(int featureNo)
        => async (context, next) =>
        {
            var services = context.HttpContext.RequestServices;
            var currentUser = services.GetRequiredService<CurrentUser>();
            var access = services.GetRequiredService<FeatureAccessService>();
            if (currentUser.IsIdentified
                && await access.GetAsync(currentUser.Id, featureNo, context.HttpContext.RequestAborted) == FeatureAccess.Disabled)
            {
                return Results.NotFound();
            }

            return await next(context);
        };

    /// <summary>
    /// ログインしている人が、管理者に使えなくされた機能の、画面側のアセンブリの名前を返す。
    /// </summary>
    /// <param name="features">足した機能。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="access">機能の利用。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>使えない機能の画面側のアセンブリの名前。</returns>
    private static async Task<string[]> GetDisabledClientAssembliesAsync(
        IReadOnlyList<IWebFeature> features, CurrentUser currentUser, FeatureAccessService access, CancellationToken cancellationToken)
    {
        var states = await access.GetAllAsync(currentUser.Id, cancellationToken);
        return [.. features
            .Where(feature => feature.FeatureNo is { } no && states.TryGetValue(no, out var state) && state == FeatureAccess.Disabled)
            .Select(feature => feature.ClientAssembly.GetName().Name ?? "")];
    }
}
