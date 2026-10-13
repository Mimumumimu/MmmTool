using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Tools.Main;

namespace MmmTool.Tools;

/// <summary>ツールの入口 (ページを登録する)</summary>
public sealed class ToolsPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    /// <remarks>保存するデータは無い。設定ページでオン・オフできる (オフの間はサイドバーに出さない)。</remarks>
    public void Register(IServiceCollection services)
    {
        services.AddFeature(ToolsFeature.Key, ToolsFeature.DisplayName);

        services.AddTransient<ToolsViewModel>();
        services.AddNavigationPage<ToolsPage>(ToolsFeature.DisplayName, "", NavigationArea.Top, ToolsFeature.Key);
    }
}
