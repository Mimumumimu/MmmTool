using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Features.Settings.FeatureList;
using MmmTool.Features.Settings.General;
using MmmTool.Features.Settings.Main;
using MmmTool.Shell;

namespace MmmTool.Features.Settings;

/// <summary>設定ページの DI 登録</summary>
public static class SettingsServiceCollectionExtensions
{
    /// <summary>設定ページの先頭に並べる部品を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 設定ページの部品は並び順の値で並ぶので、呼ぶ位置は問わない。
    /// 並び順の値は <see cref="SettingsSectionOrder"/> で決まり、「全般」→「保存先」→「機能」→ 各機能の順になる。
    /// </remarks>
    public static IServiceCollection AddSettingsSections(this IServiceCollection services)
    {
        services.AddTransient<MainWindowSettingsViewModel>();
        services.AddSettingsSection<MainWindowSettingsControl>(order: SettingsSectionOrder.General);

        services.AddTransient<FeatureListViewModel>();
        services.AddSettingsSection<FeatureListControl>(order: SettingsSectionOrder.FeatureList);
        return services;
    }

    /// <summary>設定ページを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 設定の項目は各機能が <c>AddSettingsSection</c> で部品を登録する
    /// 例はリマインダーのスヌーズ間隔
    /// このページは並び順の値の順に並べるだけ
    /// このページは特定の機能を知らない
    /// </remarks>
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddTransient<SettingsViewModel>();
        services.AddNavigationPage<SettingsPage>("設定", "", NavigationArea.Footer);
        return services;
    }
}
