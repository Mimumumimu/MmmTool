using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.CodeMaster.Core;
using MmmTool.Messages.Cache;
using MmmTool.Messages.Core;
using MmmTool.Messages.Main;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Messages;

/// <summary>メッセージの入口。ページ・読み続ける部品・起動時の準備を登録する</summary>
/// <remarks>
/// 外部へ送る処理は、MmmTool には無い。画面は、メッセージと通知の状態を DB に書くだけで、実際に送るのは MmmBatch。
/// トレイのメニューは持たない。会話は、サイドバーのページで開く。
/// </remarks>
public sealed class MessagesPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // 会話とメッセージの読み書きは、DB モードだけで使う。ローカルモードは「使えない」保存先で、DB に替えるときは、ホストが、登録のあとで置き換える
        services.AddSingleton<UnavailableMessageRepository>();
        services.AddSingleton<IMessageRepository>(provider => provider.GetRequiredService<UnavailableMessageRepository>());

        // 受け取り先を指定できる設定として登録する。送信先の設定画面の「受け取り先」の節に出る
        services.AddSingleton<IReceiveTargetSetting, MessageReceiveTarget>();

        // サーバーモード (DB モード)のときだけ使える。ローカルモードでは、サイドバー・設定の機能の一覧に出ない。管理者が無効にしたユーザーにも出ない
        services.AddFeature(MessagesFeature.Key, MessagesFeature.DisplayName,
            availableWhen: provider => provider.GetRequiredService<IMessageRepository>().IsAvailable
                && provider.IsFeatureAllowed(CodeFeatures.Messages));

        // 端末の控えと、読み続ける部品。起動時の準備でループを始め、新着を通知ダイアログで知らせる
        services.AddSingleton<MessageCacheStore>();
        services.AddSingleton<MessageSyncService>();
        services.AddStartupTask<MessagesStartup>(MessagesFeature.Key);

        services.AddTransient<MessagesViewModel>();

        // アイコンは Segoe Fluent Icons の Message で、コードは E8BD の吹き出し。表示されない文字なので、エスケープで書く
        services.AddNavigationPage<MessagesPage>(MessagesFeature.DisplayName, "", NavigationArea.Top, MessagesFeature.Key);
    }
}
