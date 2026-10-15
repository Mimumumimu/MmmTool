using Microsoft.Extensions.DependencyInjection;
using MmmBatch.ReminderSending.Core;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;

namespace MmmBatch.ReminderSending;

/// <summary>リマインダーの送信の入口 (毎分の仕事とページを登録する)</summary>
/// <remarks>
/// 送る対象の読み出し口 (<see cref="IReminderSendTargetSource"/>)と、送信の状況の保存先 (<see cref="IReminderSendStatusRepository"/>)は、ホストが DB の実装を登録する。
/// 機能に共通の送信の処理 (送る口・ログ・毎分の監視)は、ホストが <c>AddSendingCore</c> で登録する。
/// </remarks>
public sealed class ReminderSendingPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // MmmBatch の機能は、オフにできない (オン・オフの画面は持たない)。キーは、サイドバーの並びのために使う
        services.AddAlwaysOnFeature(ReminderSendingFeature.Key, ReminderSendingFeature.DisplayName);

        // 毎分の仕事 (リマインダーを時刻になったら送る)
        services.AddBatchJob<ReminderSendService>();

        // ページは、作ったあと使い回す。ページごとのスコープから作るので、ページを捨てるときに、画面と ViewModel の購読もまとめて解放される
        services.AddTransient<ReminderPlanViewModel>();
        services.AddTransient<ReminderPlanControl>();
        services.AddTransient<ReminderHistoryViewModel>();
        services.AddTransient<ReminderHistoryControl>();

        // アイコンは Segoe Fluent Icons の Calendar (E787。MmmTool のリマインダーと同じ)
        services.AddNavigationPage<ReminderSendingPage>(ReminderSendingFeature.DisplayName, "", NavigationArea.Top, ReminderSendingFeature.Key);
    }
}
