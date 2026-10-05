using Microsoft.Extensions.DependencyInjection;
using MmmTool.Features.Settings.Main;
using MmmTool.Shell;

namespace MmmTool.Features.Settings;

/// <summary>設定ページの DI 登録</summary>
public static class SettingsServiceCollectionExtensions
{
    /// <summary>設定ページを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>設定の項目は、各機能が <c>AddSettingsSection</c> で部品を登録し (例: リマインダーのスヌーズ間隔)、このページは登録順に並べるだけ。このページは特定の機能を知らない。</remarks>
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddTransient<SettingsViewModel>();
        services.AddNavigationPage<SettingsPage>("設定", "", NavigationArea.Footer);
        return services;
    }
}
