using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.NotificationChannels.ChannelEdit;
using MmmTool.NotificationChannels.ChannelList;
using MmmTool.NotificationChannels.Settings;

namespace MmmTool.NotificationChannels;

/// <summary>通知の送信先の画面を DI に登録する</summary>
public static class NotificationChannelsServiceCollectionExtensions
{
    /// <summary>送信先の画面と設定ページの通知の節を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="settingsOrder">
    /// 設定ページの先頭の「送信先」の並び順の値。
    /// 続く 2 つは 1 ずつ大きい値で、すぐあとに並ぶ。
    /// </param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 設定ページの「送信先」「通知先」「通知を受け取る機能」をこの順に登録する。
    /// 3 つともサーバーモードだけ出る。
    /// 通知先はリマインダーにも、メッセージにも、ノートにも属さない共通の設定なので、機能のキーを付けない。
    /// 機能のキーを付けると、管理者がリマインダーを無効にした人の設定ページから、3 つとも消える。
    /// そうなると、通知先を指定できず、メッセージを送れなくなる。
    /// </remarks>
    public static IServiceCollection AddNotificationChannelScreens(this IServiceCollection services, int settingsOrder)
    {
        services.AddSingleton<INotificationChannelDialogService, NotificationChannelDialogService>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<NotificationChannelListWindow>();
        services.AddTransient<NotificationChannelListViewModel>();
        services.AddTransient<NotificationChannelEditWindow>();
        services.AddTransient<NotificationChannelEditViewModel>();

        services.AddTransient<NotificationChannelSettingsViewModel>();
        services.AddSettingsSection<NotificationChannelSettingsControl>(order: settingsOrder);

        services.AddTransient<NotificationTargetSettingsViewModel>();
        services.AddSettingsSection<NotificationTargetSettingsControl>(order: settingsOrder + 1);

        // 「通知を受け取る」のオン・オフとして登録された分を並べて、機能の名前は持たない
        services.AddTransient<NotificationFeatureSettingsViewModel>();
        services.AddSettingsSection<NotificationFeatureSettingsControl>(order: settingsOrder + 2);
        return services;
    }
}
