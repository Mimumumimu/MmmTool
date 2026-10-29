using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Tray;
using MmmTool.CodeMaster.Core;
using MmmTool.NotificationChannels.Core;
using MmmTool.Reminders.BulkInput;
using MmmTool.Reminders.ChannelEdit;
using MmmTool.Reminders.ChannelList;
using MmmTool.Reminders.Core;
using MmmTool.Reminders.Core.Json;
using MmmTool.Reminders.Input;
using MmmTool.Reminders.List;
using MmmTool.Users.Core;
using MmmTool.Reminders.Main;
using MmmTool.Reminders.Settings;

namespace MmmTool.Reminders;

/// <summary>リマインダーの入口 (データ・時刻監視・各画面・トレイメニューを登録する)</summary>
public sealed class RemindersPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    /// <remarks>保存先は JSON。設定が DB のときだけ、ホストが、登録のあとで SQL Server の保存先に置き換える (<c>MmmTool.Data.SqlServer</c>)。</remarks>
    public void Register(IServiceCollection services)
    {
        // オフにできない機能で、設定の「機能」の一覧では、並び順だけ使う。管理者が無効にしたユーザー (サーバーモード)には、サイドバー・設定の機能の一覧に出ない
        services.AddAlwaysOnFeature(RemindersFeature.Key, RemindersFeature.DisplayName,
            provider => provider.IsFeatureAllowed(CodeFeatures.Reminders));

        // 保存先 (JSON。DB に置き換えるために、実体の型でも取り出せるようにする)
        services.AddSingleton<JsonReminderRepository>();
        services.AddSingleton<IReminderRepository>(provider => provider.GetRequiredService<JsonReminderRepository>());

        // 送信先と送信設定 (DB モードだけで使う。ローカルモードは「使えない」保存先。DB に替えるときは、ホストが、登録のあとで置き換える)
        services.AddSingleton<UnavailableNotificationChannelRepository>();
        services.AddSingleton<INotificationChannelRepository>(provider => provider.GetRequiredService<UnavailableNotificationChannelRepository>());
        services.AddSingleton<UnavailableReminderSendSettingRepository>();
        services.AddSingleton<IReminderSendSettingRepository>(provider => provider.GetRequiredService<UnavailableReminderSendSettingRepository>());

        // 変更の通知 (Changed)を各画面と時刻監視で共有するため、アプリ全体で 1 つ。監視は Host の破棄時に止まる
        services.AddSingleton<ReminderService>();
        services.AddSingleton<ReminderMonitor>();
        services.AddSingleton<ReminderSettingsService>();
        services.AddSingleton<ReminderChannelLoader>();

        services.AddSingleton<IReminderDialogService, ReminderDialogService>();
        // メイン画面はアプリ内で 1 枚だけ (開いていれば前面に出す)
        services.AddSingleton<ReminderWindowService>();
        services.AddTrayMenuSource<ReminderTrayMenuSource>(RemindersFeature.Key);

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<ReminderInputWindow>();
        services.AddTransient<ReminderBulkInputWindow>();
        services.AddTransient<ReminderListWindow>();
        services.AddTransient<ReminderMainWindow>();
        services.AddTransient<ReminderInputViewModel>();
        services.AddTransient<ReminderBulkInputViewModel>();
        services.AddTransient<ReminderListViewModel>();
        services.AddTransient<ReminderMainViewModel>();
        services.AddTransient<NotificationChannelListWindow>();
        services.AddTransient<NotificationChannelListViewModel>();
        services.AddTransient<NotificationChannelEditWindow>();
        services.AddTransient<NotificationChannelEditViewModel>();

        // 設定ページに並べる設定 (スヌーズ間隔)
        services.AddTransient<ReminderSettingsViewModel>();
        services.AddSettingsSection<ReminderSettingsControl>(RemindersFeature.Key);

        // 設定ページの「送信先」 (サーバーモードだけ出る。送信先の一覧画面を開く)
        services.AddTransient<NotificationChannelSettingsViewModel>();
        services.AddSettingsSection<NotificationChannelSettingsControl>(RemindersFeature.Key);

        // 設定ページの「受け取り先」は、サーバーモードだけ出る。通知の受け取り先は、どの機能にも属さない共通の設定 1 つ
        services.AddTransient<ReceiveTargetSettingsViewModel>();
        services.AddSettingsSection<ReceiveTargetSettingsControl>(RemindersFeature.Key);

        // 設定ページの「通知を受け取る機能」は、サーバーモードだけ出る。「通知を受け取る」のオン・オフとして登録された分を並べて、機能の名前は持たない
        services.AddTransient<NotificationFeatureSettingsViewModel>();
        services.AddSettingsSection<NotificationFeatureSettingsControl>(RemindersFeature.Key);

        // サイドバーの「リマインダー」(一覧。ウィンドウの一覧と中身を共有する)。オフにできない機能なので、機能のキーは、並び順のためだけに渡す
        services.AddNavigationPage<ReminderListPage>(RemindersFeature.DisplayName, "", NavigationArea.Top, RemindersFeature.Key);

        services.AddStartupTask<ReminderStartup>();
    }
}
