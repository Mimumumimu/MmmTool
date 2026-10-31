using Microsoft.Extensions.DependencyInjection;
using MmmBatch.NoteSending.Core;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;

namespace MmmBatch.NoteSending;

/// <summary>ノートの送信の入口 (10 秒おきの仕事とページを登録する)</summary>
/// <remarks>
/// 更新されたノートと通知する相手の読み出し口 (<see cref="INoteSendSource"/>)は、ホストが DB の実装を登録する。
/// 機能に共通の送信の処理 (送る口・ログ・定期の監視)は、ホストが <c>AddSendingCore</c> で登録する。
/// </remarks>
public sealed class NoteSendingPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // MmmBatch の機能は、オフにできない。オン・オフの画面は持たない。キーは、サイドバーの並びのために使う
        services.AddAlwaysOnFeature(NoteSendingFeature.Key, NoteSendingFeature.DisplayName);

        // 10 秒おきの仕事。他の人が作成・更新したノートを送る
        services.AddBatchJob<NoteSendService>();

        // ページは、作ったあと使い回す。ページごとのスコープから作るので、ページを捨てるときに、画面と ViewModel の購読もまとめて解放される
        services.AddTransient<NoteHistoryViewModel>();
        services.AddTransient<NoteHistoryControl>();

        // アイコンは Segoe Fluent Icons の QuickNote で、コードは E70B の付箋とペン。MmmTool のノートと同じ
        services.AddNavigationPage<NoteSendingPage>(NoteSendingFeature.DisplayName, "", NavigationArea.Top, NoteSendingFeature.Key);
    }
}
