using System.Reflection;
using MmmTool.Data.SqlServer.Messages;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;
using MmmToolWeb.Features;
using MmmToolWeb.Messages.Client;

namespace MmmToolWeb.Messages;

/// <summary>
/// メッセージの Web 版のサーバー側。会話とメッセージの読み書きは、MmmTool と同じ <c>SqlServerMessageRepository</c> を使う。
/// </summary>
/// <remarks>
/// 外部へ送る処理は、ここには無い。メッセージと通知の状態の行を DB に書くだけで、実際に送るのは MmmBatch。
/// 会話を開いている端末に、更新を知らせる常駐の処理 (<see cref="MessageChangeWatcher"/>)を持つ。
/// </remarks>
public sealed class MessagesWebFeature : IWebFeature
{
    /// <inheritdoc />
    public Assembly ClientAssembly => typeof(MessagesClient).Assembly;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        // 保存先は、要求ごとに作る。今のユーザーが、要求ごとに違うため
        services.AddScoped<IMessageRepository, SqlServerMessageRepository>();
        services.AddScoped<IAppUserSettingRepository, SqlServerAppUserSettingRepository>();

        // 更新を、会話を開いている端末に知らせる。DB を調べるのも、知らせる口も、アプリで 1 つ
        services.AddSingleton<SqlServerMessageChangeReader>();
        services.AddSingleton<MessageChangeHub>();
        services.AddHostedService<MessageChangeWatcher>();

        // メッセージの通知を押したときに、会話を開く
        services.AddScoped<INotificationDestination, MessageNotificationDestination>();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, MessagesJsonContext.Default));
    }

    /// <inheritdoc />
    public void MapEndpoints(RouteGroupBuilder api) => MessageEndpoints.Map(api.MapGroup("/messages"));
}
