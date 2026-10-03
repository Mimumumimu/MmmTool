using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Tray;
using MmmTool.Core.Links;
using MmmTool.Core.Links.Json;
using MmmTool.Features.Links.Main;
using MmmTool.Shell;

namespace MmmTool.Features.Links;

/// <summary>リンクの DI 登録</summary>
public static class LinksServiceCollectionExtensions
{
    /// <summary>リンク（編集ページ・トレイのリンクメニュー）を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション（続けて登録するため）</returns>
    /// <remarks>保存先はローカル専用の JSON。</remarks>
    public static IServiceCollection AddLinks(this IServiceCollection services)
    {
        // 保存先
        services.AddSingleton<ILinkRepository, JsonLinkRepository>();

        // 最後に読み込み・保存した構成を、編集ページとトレイのリンクメニューで共有するため、アプリ全体で 1 つ
        services.AddSingleton<LinkMenuService>();
        services.AddSingleton<ITrayMenuSource, LinkTrayMenuSource>();

        services.AddTransient<LinkEditorViewModel>();
        services.AddNavigationPage<LinkEditorPage>("リンク", "", NavigationArea.Top);

        services.AddStartupTask<LinkStartup>();
        return services;
    }
}
