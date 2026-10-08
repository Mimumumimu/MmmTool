using Microsoft.Extensions.DependencyInjection;
using MmmBatch.Sending.Core;

namespace MmmBatch.Sending;

/// <summary>
/// リマインダーの送信 (送る処理と、送信の状況の画面)を DI に登録する。
/// </summary>
public static class SendingServiceCollectionExtensions
{
    /// <summary>リマインダーの送信を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="logDirectory">送信のログを置くフォルダー (<c>Data/Logs</c>)</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 送る対象の読み出し口と、送信の状況の保存先 (DB の実装)は、ホストが登録する。
    /// 画面 (<see cref="SendMainControl"/>と、その中の今日の送信予定・履歴)と ViewModel はアプリ全体で 1 つ (メインウィンドウが持ち続ける)。
    /// </remarks>
    public static IServiceCollection AddSending(this IServiceCollection services, string logDirectory)
    {
        services.AddSendingCore(logDirectory);
        services.AddSingleton<SendPlanViewModel>();
        services.AddSingleton<SendPlanControl>();
        services.AddSingleton<SendHistoryViewModel>();
        services.AddSingleton<SendHistoryControl>();
        services.AddSingleton<SendMainControl>();
        return services;
    }
}
