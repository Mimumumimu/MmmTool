using Microsoft.Extensions.DependencyInjection;
using MmmTool.Core.Backlog;
using MmmTool.Features.Backlog.Main;
using MmmTool.Features.Backlog.Settings;
using MmmTool.Shell;

namespace MmmTool.Features.Backlog;

/// <summary>Backlog 連携の DI 登録</summary>
public static class BacklogServiceCollectionExtensions
{
    /// <summary>Backlog 連携 (ページ・通信・設定の部品)を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 保存するデータは、連携用パス (URL。設定ストア)と API キー (秘密の保管庫)だけ。専用のファイルは作らない。
    /// 設定ページでオン・オフできる (オフの間は、サイドバーにも設定ページにも出さない)。
    /// </remarks>
    public static IServiceCollection AddBacklog(this IServiceCollection services)
    {
        services.AddFeature(BacklogFeature.Key, BacklogFeature.DisplayName);

        // HttpClient をアプリ全体で 1 つにするため Singleton (Host の破棄で Dispose される)
        services.AddSingleton<BacklogClient>();
        services.AddSingleton<BacklogSettingsService>();

        services.AddTransient<BacklogViewModel>();
        services.AddNavigationPage<BacklogPage>(BacklogFeature.DisplayName, "", NavigationArea.Top, BacklogFeature.Key);

        // 設定ページに並べる設定 (API キー)
        services.AddTransient<BacklogSettingsViewModel>();
        services.AddSettingsSection<BacklogSettingsControl>(BacklogFeature.Key);
        return services;
    }
}
