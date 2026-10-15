using Microsoft.Extensions.DependencyInjection;
using MmmBatch.Sending.Core.Notifiers;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 機能に共通の送信の処理 (UI に依存しない部分)を DI に登録する。
/// </summary>
public static class SendingCoreServiceCollectionExtensions
{
    /// <summary>通信のタイムアウト</summary>
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    /// <summary>接続を使い回す時間 (DNS の変更に追従するため、長く持たない)</summary>
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(5);

    /// <summary>機能に共通の送信の処理を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="logDirectory">送信のログを置くフォルダー (<c>Data/Logs</c>)</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// ホストが 1 回だけ呼ぶ。通信の <see cref="HttpClient"/> は、アプリ全体で 1 つ (接続を使い回すため。NuGet は足さない)。
    /// 毎分動かす仕事は、各機能が <see cref="AddBatchJob{TJob}"/> で登録する。<see cref="BatchMonitor"/> は、登録された仕事を動かす。
    /// </remarks>
    public static IServiceCollection AddSendingCore(this IServiceCollection services, string logDirectory)
    {
        services.AddSingleton(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = ConnectionLifetime }) { Timeout = HttpTimeout });
        services.AddSingleton<INotifier, NtfyNotifier>();
        services.AddSingleton<INotifier, DiscordWebhookNotifier>();
        // ログのファイルに書きながら、起動してから送った分をメモリにも持つ (画面の「履歴」用。ファイルは読まない)
        services.AddSingleton(new RecentSendLog(new FileSendLog(logDirectory)));
        services.AddSingleton<ISendLog>(provider => provider.GetRequiredService<RecentSendLog>());
        services.AddSingleton<ISendHistory>(provider => provider.GetRequiredService<RecentSendLog>());
        services.AddSingleton<BatchAlertService>();
        services.AddSingleton<BatchMonitor>();
        return services;
    }

    /// <summary>毎分動かす仕事を登録する</summary>
    /// <typeparam name="TJob">仕事の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 仕事の型そのもの (画面が、変更の通知・再送に使う)と、<see cref="IBatchJob"/>(<see cref="BatchMonitor"/>が動かす)の両方で、同じインスタンスを受け取れる。
    /// 仕事は、登録した順に動く。
    /// </remarks>
    public static IServiceCollection AddBatchJob<TJob>(this IServiceCollection services)
        where TJob : class, IBatchJob
    {
        services.AddSingleton<TJob>();
        services.AddSingleton<IBatchJob>(provider => provider.GetRequiredService<TJob>());
        return services;
    }
}
