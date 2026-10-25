using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.CodeMaster;
using MmmTool.CodeMaster.Core;
using MmmTool.DayContent.Core;
using MmmTool.Users.Core;
using MmmTool.WorkItems.Core;
using MmmTool.WorkItems.Main;
using MmmTool.WorkItems.Move;
using MmmTool.WorkItems.ProjectManager;
using MmmTool.WorkItems.ProjectPicker;
using MmmTool.WorkItems.Report;
using MmmTool.WorkItems.Schedule;
using MmmTool.WorkItems.Settings;

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
        // 管理者が無効にしたユーザー (サーバーモード)には、サイドバー・設定の機能の一覧に出ない
        services.AddFeature(WorkItemsFeature.Key, WorkItemsFeature.DisplayName,
            availableWhen: provider => provider.IsFeatureAllowed(CodeFeatures.WorkItems));

        // 更新してよいかの判定 (今は誰でも更新できる。担当者・リーダーだけに絞る実装は、この行を差し替える)
        services.AddSingleton<IWorkItemEditPolicy, AnyoneCanEdit>();

        // 最後に読み込み・保存した内容と、変更の通知 (Changed)を、ページ・起動時の読み込みで共有するため、アプリ全体で 1 つ
        services.AddSingleton<WorkItemService>();

        // 作業管理とスケジュールの、起動時の表示の設定 (画面を作るときに読む)
        services.AddSingleton<WorkItemDisplaySettings>();

        // 画面で選んだ状態 (ページを外す・戻す・機能をオフにしてオンに戻すときに、ページが作り直されても引き継ぐため、ページの外で持つ)
        services.AddSingleton<WorkItemViewState>();
        services.AddSingleton<ScheduleViewState>();

        // 設定ページの「作業管理」 (起動時の表示)
        services.AddTransient<WorkItemSettingsViewModel>();
        services.AddSettingsSection<WorkItemSettingsControl>(WorkItemsFeature.Key);

        // ノートの画面に、作業報告を出す。ノートは、契約 IDayContentSource だけを知る。作業管理の変更は、ノートへ知らせる
        services.AddSingleton<IDayContentSource, LiveWorkReportDayContentSource>();

        // 設定ページの「選択肢」から、進捗度の名前 (数値の後ろの括弧の中)を直せる。保存したら、開いている画面の表記を読み直す
        services.AddCodeCategoryEditor(provider => WorkCodes.ProgressNameEditor(
            WorkItemsFeature.Key,
            () => provider.GetRequiredService<WorkItemService>().RefreshNowAsync()));

        services.AddSingleton<IWorkItemDialogService, WorkItemDialogService>();
        // 閉じたダイアログは再表示できないので、開くたびに作る
        services.AddTransient<MoveDialog>();
        services.AddTransient<ProjectManagerWindow>();
        services.AddTransient<ProjectManagerViewModel>();
        services.AddTransient<WorkReportWindow>();
        services.AddTransient<WorkReportViewModel>();

        services.AddTransient<ProjectPickerViewModel>();
        services.AddTransient<ScheduleViewModel>();
        services.AddTransient<WorkItemViewModel>();
        services.AddNavigationPage<WorkItemPage>(WorkItemsFeature.DisplayName, "", NavigationArea.Top, WorkItemsFeature.Key, canDetach: true);
        // スケジュールは、作業管理の子の項目 (作業管理がオフの間は出さない)
        services.AddNavigationChildPage<SchedulePage, WorkItemPage>("スケジュール", "", WorkItemsFeature.Key, canDetach: true);

        services.AddStartupTask<WorkItemStartup>(WorkItemsFeature.Key);
    }
}
