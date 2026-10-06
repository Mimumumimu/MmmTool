using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Tray;
using MmmTool.Reminders.Core;
using MmmTool.Reminders.Core.Json;
using MmmTool.Reminders.Input;
using MmmTool.Reminders.List;
using MmmTool.Reminders.Main;
using MmmTool.Reminders.Settings;

namespace MmmTool.Reminders;

/// <summary>リマインダーの入口 (データ・時刻監視・各画面・トレイメニューを登録する)</summary>
public sealed class RemindersPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    /// <remarks>保存先は JSON (将来 DB に替える可能性がある。替えるときは「保存先」の行だけを差し替える)。</remarks>
    public void Register(IServiceCollection services)
    {
        // 保存先
        services.AddSingleton<IReminderRepository, JsonReminderRepository>();

        // 変更の通知 (Changed)を各画面と時刻監視で共有するため、アプリ全体で 1 つ。監視は Host の破棄時に止まる
        services.AddSingleton<ReminderService>();
        services.AddSingleton<ReminderMonitor>();
        services.AddSingleton<ReminderSettingsService>();

        services.AddSingleton<IReminderDialogService, ReminderDialogService>();
        // メイン画面はアプリ内で 1 枚だけ (開いていれば前面に出す)
        services.AddSingleton<ReminderWindowService>();
        services.AddTrayMenuSource<ReminderTrayMenuSource>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<ReminderInputWindow>();
        services.AddTransient<ReminderListWindow>();
        services.AddTransient<ReminderMainWindow>();
        services.AddTransient<ReminderInputViewModel>();
        services.AddTransient<ReminderListViewModel>();
        services.AddTransient<ReminderMainViewModel>();

        // 設定ページに並べる設定 (スヌーズ間隔)
        services.AddTransient<ReminderSettingsViewModel>();
        services.AddSettingsSection<ReminderSettingsControl>();

        services.AddStartupTask<ReminderStartup>();
    }
}
