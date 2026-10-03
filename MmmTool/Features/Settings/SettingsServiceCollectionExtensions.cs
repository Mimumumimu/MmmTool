using Microsoft.Extensions.DependencyInjection;
using MmmTool.Shell;

namespace MmmTool.Features.Settings;

/// <summary>設定ページの DI 登録</summary>
public static class SettingsServiceCollectionExtensions
{
    /// <summary>設定ページを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション（続けて登録するため）</returns>
    /// <remarks>項目の値は各機能のサービスが持つ（スヌーズ間隔はリマインダー）。そのサービスは各機能の登録で入る。</remarks>
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddTransient<SettingsViewModel>();
        services.AddNavigationPage<SettingsPage>("設定", "", NavigationArea.Footer);
        return services;
    }
}
