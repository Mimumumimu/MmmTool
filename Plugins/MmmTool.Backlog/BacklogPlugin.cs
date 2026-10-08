using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Backlog.Core;
using MmmTool.Backlog.Main;
using MmmTool.Backlog.Settings;

namespace MmmTool.Backlog;

/// <summary>Backlog 連携の入口 (ページ・通信・設定の部品を登録する)</summary>
public sealed class BacklogPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    /// <remarks>
    /// 保存するデータは、連携用パス (URL。設定ストア)と API キー (秘密の保管庫)だけ。専用のファイルは作らない。
    /// 設定ページでオン・オフできる (オフの間は、サイドバーにも設定ページにも出さない)。
    /// </remarks>
    public void Register(IServiceCollection services)
    {
        services.AddFeature(BacklogFeature.Key, BacklogFeature.DisplayName, defaultEnabled: false);

        // HttpClient をアプリ全体で 1 つにするため Singleton (Host の破棄で Dispose される)
        services.AddSingleton<BacklogClient>();
        services.AddSingleton<BacklogSettingsService>();

        services.AddTransient<BacklogViewModel>();
        services.AddNavigationPage<BacklogPage>(BacklogFeature.DisplayName, "", NavigationArea.Top, BacklogFeature.Key);

        // 設定ページに並べる設定 (API キー)
        services.AddTransient<BacklogSettingsViewModel>();
        services.AddSettingsSection<BacklogSettingsControl>(BacklogFeature.Key);
    }
}
