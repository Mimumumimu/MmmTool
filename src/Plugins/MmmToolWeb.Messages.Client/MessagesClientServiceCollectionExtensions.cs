using Microsoft.Extensions.DependencyInjection;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Messages.Client;

/// <summary>
/// メッセージの画面を、ブラウザ側のアプリに足す。
/// </summary>
public static class MessagesClientServiceCollectionExtensions
{
    /// <summary>アイコンの形。ふきだしの絵。</summary>
    private const string IconPath = "M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zM6 9h12v2H6V9zm8 5H6v-2h8v2zm4-6H6V6h12v2z";

    /// <summary>
    /// メッセージの画面と、API を呼ぶ部品を登録する。
    /// </summary>
    /// <param name="services">登録先。</param>
    /// <returns>続けて呼ぶための <paramref name="services"/>。</returns>
    public static IServiceCollection AddMessagesClient(this IServiceCollection services)
    {
        services.AddScoped<MessagesClient>();
        return services.AddClientFeature(new ClientFeature("メッセージ", IconPath, MessagesPage.Route, typeof(MessagesPage).Assembly));
    }
}
