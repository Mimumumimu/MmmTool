using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Tray;
using MmmTool.Messages.Core;
using MmmTool.Messages.Main;

namespace MmmTool.Messages;

/// <summary>メッセージ送信の入口 (ページ・ウィンドウ・トレイメニューを登録する)</summary>
/// <remarks>
/// 外部へ送る処理は、MmmTool には無い (決まり)。画面は、送信の依頼を DB に書くだけで、実際に送るのは MmmBatch。
/// </remarks>
public sealed class MessagesPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // 宛先の読み出しと送信依頼の保存 (DB モードだけで使う。ローカルモードは「使えない」保存先。DB に替えるときは、ホストが、登録のあとで置き換える)
        services.AddSingleton<UnavailableMessageSendRequestRepository>();
        services.AddSingleton<IMessageSendRequestRepository>(provider => provider.GetRequiredService<UnavailableMessageSendRequestRepository>());

        // サーバーモード (DB モード)のときだけ使える。ローカルモードでは、サイドバー・トレイ・設定の機能の一覧に出ない
        services.AddFeature(MessagesFeature.Key, MessagesFeature.DisplayName,
            availableWhen: provider => provider.GetRequiredService<IMessageSendRequestRepository>().IsAvailable);

        services.AddSingleton<MessageWindowService>();
        services.AddTrayMenuSource<MessageTrayMenuSource>(MessagesFeature.Key);

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<MessageSendWindow>();
        services.AddTransient<MessageSendViewModel>();

        // アイコンは Segoe Fluent Icons の Message (E8BD。吹き出し)
        services.AddNavigationPage<MessageSendPage>(MessagesFeature.DisplayName, "", NavigationArea.Top, MessagesFeature.Key);
    }
}
