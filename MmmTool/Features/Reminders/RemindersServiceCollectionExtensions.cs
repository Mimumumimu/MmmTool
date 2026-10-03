using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Tray;
using MmmTool.Core.Reminders;
using MmmTool.Core.Reminders.Json;
using MmmTool.Features.Reminders.Input;
using MmmTool.Features.Reminders.List;
using MmmTool.Features.Reminders.Main;
using MmmTool.Shell;

namespace MmmTool.Features.Reminders;

/// <summary>リマインダーの DI 登録</summary>
public static class RemindersServiceCollectionExtensions
{
    /// <summary>リマインダー（データ・時刻監視・各画面・トレイメニュー）を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション（続けて登録するため）</returns>
    /// <remarks>保存先は JSON（将来 DB に替える可能性がある。替えるときは「保存先」の行だけを差し替える）。</remarks>
    public static IServiceCollection AddReminders(this IServiceCollection services)
    {
        // 保存先
        services.AddSingleton<IReminderRepository, JsonReminderRepository>();

        // 変更の通知（Changed）を各画面と時刻監視で共有するため、アプリ全体で 1 つ。監視は Host の破棄時に止まる
        services.AddSingleton<ReminderService>();
        services.AddSingleton<ReminderMonitor>();
        services.AddSingleton<ReminderSettingsService>();

        services.AddSingleton<IReminderDialogService, ReminderDialogService>();
        // メイン画面はアプリ内で 1 枚だけ（開いていれば前面に出す）
        services.AddSingleton<ReminderWindowService>();
        services.AddSingleton<ITrayMenuSource, ReminderTrayMenuSource>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<ReminderInputWindow>();
        services.AddTransient<ReminderListWindow>();
        services.AddTransient<ReminderMainWindow>();
        services.AddTransient<ReminderInputViewModel>();
        services.AddTransient<ReminderListViewModel>();
        services.AddTransient<ReminderMainViewModel>();

        // 設定ページに並べる設定（スヌーズ間隔）
        services.AddTransient<ReminderSettingsViewModel>();
        services.AddSettingsSection<ReminderSettingsControl>();

        services.AddStartupTask<ReminderStartup>();
        return services;
    }
}
