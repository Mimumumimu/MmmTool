using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;

namespace MmmToolAdmin.CommonSettings;

/// <summary>共通設定の入口で、ページを登録する</summary>
/// <remarks>
/// ページに並ぶ設定の部品は、各機能が <c>AddSettingsSection</c> で登録する。このプラグインは、個々の部品を知らない。
/// </remarks>
public sealed class CommonSettingsPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // MmmToolAdmin の機能は、オフにできず、オン・オフの画面も持たない。キーは、サイドバーの並びのために使う
        services.AddAlwaysOnFeature(CommonSettingsFeature.Key, CommonSettingsFeature.DisplayName);

        // アイコンは Segoe Fluent Icons の Tag で、E8EC
        services.AddNavigationPage<CommonSettingsPage>(CommonSettingsFeature.DisplayName, "", NavigationArea.Top, CommonSettingsFeature.Key);
    }
}
