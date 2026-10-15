using Microsoft.Extensions.DependencyInjection;
using MmmBatch.MessageSending.Core;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;

namespace MmmBatch.MessageSending;

/// <summary>メッセージの送信の入口 (毎分の仕事とページを登録する)</summary>
/// <remarks>
/// 送信依頼の読み出しと状態の保存先 (<see cref="IMessageSendStore"/>)は、ホストが DB の実装を登録する。
/// 機能に共通の送信の処理 (送る口・ログ・毎分の監視)は、ホストが <c>AddSendingCore</c> で登録する。
/// </remarks>
public sealed class MessageSendingPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // MmmBatch の機能は、オフにできない (オン・オフの画面は持たない)。キーは、サイドバーの並びのために使う
        services.AddAlwaysOnFeature(MessageSendingFeature.Key, MessageSendingFeature.DisplayName);

        // 毎分の仕事 (MmmTool で依頼されたメッセージを送る)
        services.AddBatchJob<MessageSendService>();

        // ページは、作ったあと使い回す。ページごとのスコープから作るので、ページを捨てるときに、画面と ViewModel の購読もまとめて解放される
        services.AddTransient<MessageRecentViewModel>();
        services.AddTransient<MessageRecentControl>();
        services.AddTransient<MessageHistoryViewModel>();
        services.AddTransient<MessageHistoryControl>();

        // アイコンは Segoe Fluent Icons の Message (E8BD。吹き出し。MmmTool のメッセージ送信と同じ)
        services.AddNavigationPage<MessageSendingPage>(MessageSendingFeature.DisplayName, "", NavigationArea.Top, MessageSendingFeature.Key);
    }
}
