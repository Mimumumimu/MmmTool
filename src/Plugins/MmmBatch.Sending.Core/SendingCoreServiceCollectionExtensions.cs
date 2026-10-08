using Microsoft.Extensions.DependencyInjection;
using MmmBatch.Sending.Core.Notifiers;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 送信の処理 (UI に依存しない部分)を DI に登録する。
/// </summary>
public static class SendingCoreServiceCollectionExtensions
{
    /// <summary>通信のタイムアウト</summary>
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    /// <summary>接続を使い回す時間 (DNS の変更に追従するため、長く持たない)</summary>
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(5);

    /// <summary>送信の処理を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 通信の <see cref="HttpClient"/> は、アプリ全体で 1 つ (接続を使い回すため。NuGet は足さない)。
    /// 送る対象の読み出し口 (<see cref="ISendTargetSource"/>)と、送信の状況の保存先 (<see cref="IReminderSendStatusRepository"/>)は、DB の実装が登録する。
    /// </remarks>
    public static IServiceCollection AddSendingCore(this IServiceCollection services)
    {
        services.AddSingleton(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = ConnectionLifetime }) { Timeout = HttpTimeout });
        services.AddSingleton<INotifier, NtfyNotifier>();
        services.AddSingleton<INotifier, DiscordWebhookNotifier>();
        services.AddSingleton<ReminderSendService>();
        services.AddSingleton<SendMonitor>();
        return services;
    }
}
