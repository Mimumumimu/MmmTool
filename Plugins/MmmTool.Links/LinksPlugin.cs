using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Tray;
using MmmTool.Links.Core;
using MmmTool.Links.Core.Json;
using MmmTool.Links.Main;

namespace MmmTool.Links;

/// <summary>リンク機能の入口 (編集ページ・トレイのリンクメニューを登録する)</summary>
/// <remarks>保存先はローカル専用の JSON。</remarks>
public sealed class LinksPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // 保存先
        services.AddSingleton<ILinkRepository, JsonLinkRepository>();

        // 最後に読み込み・保存した構成を、編集ページとトレイのリンクメニューで共有するため、アプリ全体で 1 つ
        services.AddSingleton<LinkMenuService>();
        services.AddTrayMenuSource<LinkTrayMenuSource>();

        services.AddTransient<LinkEditorViewModel>();
        services.AddNavigationPage<LinkEditorPage>("リンク", "", NavigationArea.Top);

        services.AddStartupTask<LinkStartup>();
    }
}
