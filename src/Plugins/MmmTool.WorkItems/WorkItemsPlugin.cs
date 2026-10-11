using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.WorkItems.Core;
using MmmTool.WorkItems.Main;
using MmmTool.WorkItems.Move;
using MmmTool.WorkItems.ProjectManager;
using MmmTool.WorkItems.ProjectPicker;

namespace MmmTool.WorkItems;

/// <summary>作業管理の入口 (サービス・ページ・ダイアログを登録する。保存先は、ホストが設定で選んで登録する)</summary>
public sealed class WorkItemsPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    /// <remarks>
    /// 保存先 (<see cref="IWorkItemRepository"/>)は登録しない。ローカルは SQLite、DB モードは SQL Server で、ホストが設定 (<c>Database.Mode</c>)で選んで登録する。
    /// 設定ページでオン・オフできる (オフの間は、サイドバーに出さず、起動時の読み込みもしない)。
    /// </remarks>
    public void Register(IServiceCollection services)
    {
        services.AddFeature(WorkItemsFeature.Key, WorkItemsFeature.DisplayName, defaultEnabled: false);

        // 更新してよいかの判定 (今は誰でも更新できる。担当者・リーダーだけに絞る実装は、この行を差し替える)
        services.AddSingleton<IWorkItemEditPolicy, AnyoneCanEdit>();

        // 最後に読み込み・保存した内容と、変更の通知 (Changed)を、ページ・起動時の読み込みで共有するため、アプリ全体で 1 つ
        services.AddSingleton<WorkItemService>();

        services.AddSingleton<IWorkItemDialogService, WorkItemDialogService>();
        // 閉じたダイアログは再表示できないので、開くたびに作る
        services.AddTransient<MoveDialog>();
        services.AddTransient<ProjectManagerWindow>();
        services.AddTransient<ProjectManagerViewModel>();

        services.AddTransient<ProjectPickerViewModel>();
        services.AddTransient<WorkItemViewModel>();
        services.AddNavigationPage<WorkItemPage>(WorkItemsFeature.DisplayName, "", NavigationArea.Top, WorkItemsFeature.Key);

        services.AddStartupTask<WorkItemStartup>(WorkItemsFeature.Key);
    }
}
