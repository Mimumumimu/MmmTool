using Microsoft.Extensions.DependencyInjection;

namespace MmmTool.Users.Core;

/// <summary>
/// 機能の利用 (<see cref="FeatureAccess"/>)を、機能の登録から使うための拡張メソッド。
/// </summary>
public static class FeatureAccessExtensions
{
    /// <summary>今のユーザーに、機能が許されているか</summary>
    /// <param name="provider">DI のサービスプロバイダー</param>
    /// <param name="featureNo">機能番号</param>
    /// <returns>無効でなければ true。<c>AddFeature</c> の使える条件に渡す</returns>
    public static bool IsFeatureAllowed(this IServiceProvider provider, int featureNo)
        => provider.GetRequiredService<FeatureAccessCache>().Get(featureNo) != FeatureAccess.Disabled;
}
