using Microsoft.Extensions.DependencyInjection;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notifications.Client;

/// <summary>
/// 通知の画面を、ブラウザ側のアプリに足す。
/// </summary>
public static class NotificationsClientServiceCollectionExtensions
{
    /// <summary>アイコンの形。ベルの絵。</summary>
    private const string IconPath = "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.89 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z";

    /// <summary>
    /// 通知の画面と、API を呼ぶ部品を登録する。
    /// </summary>
    /// <param name="services">登録先。</param>
    /// <returns>続けて呼ぶための <paramref name="services"/>。</returns>
    public static IServiceCollection AddNotificationsClient(this IServiceCollection services)
    {
        services.AddScoped<NotificationsClient>();
        return services.AddClientFeature(
            new ClientFeature("通知", IconPath, NotificationsPage.Route, typeof(NotificationsPage).Assembly, typeof(NotificationsSettingsSection)));
    }
}
