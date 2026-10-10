using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.WorkItems.Core;
using MmmTool.WorkItems.Core.Json;
using MmmTool.WorkItems.Main;
using MmmTool.WorkItems.Move;
using MmmTool.WorkItems.ProgressLabels;

namespace MmmTool.WorkItems;

/// <summary>作業リストの入口 (保存先・サービス・ページ・ダイアログを登録する)</summary>
public sealed class WorkItemsPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    /// <remarks>
    /// 保存先は JSON。設定ページでオン・オフできる (オフの間は、サイドバーに出さず、起動時の読み込みもしない)。
    /// </remarks>
    public void Register(IServiceCollection services)
    {
        services.AddFeature(WorkItemsFeature.Key, WorkItemsFeature.DisplayName);

        // 保存先 (DB に替えるときは、この行だけを差し替える)
        services.AddSingleton<IWorkItemRepository, JsonWorkItemRepository>();

        // 最後に読み込み・保存した内容と、変更の通知 (Changed)を、ページ・起動時の読み込みで共有するため、アプリ全体で 1 つ
        services.AddSingleton<WorkItemService>();

        services.AddSingleton<IWorkItemDialogService, WorkItemDialogService>();
        // 閉じたダイアログは再表示できないので、開くたびに作る
        services.AddTransient<MoveDialog>();
        services.AddTransient<ProgressLabelsDialog>();

        services.AddTransient<WorkItemViewModel>();
        services.AddNavigationPage<WorkItemPage>(WorkItemsFeature.DisplayName, "", NavigationArea.Top, WorkItemsFeature.Key);

        services.AddStartupTask<WorkItemStartup>(WorkItemsFeature.Key);
    }
}
