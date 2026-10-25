using System.Reflection;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.NotificationChannels.Core;
using MmmToolWeb.Features;
using MmmToolWeb.Notifications.Client;

namespace MmmToolWeb.Notifications;

/// <summary>
/// 通知を受け取る端末の登録の、Web 版のサーバー側。登録は、MmmTool と同じ <c>SqlServerNotificationChannelRepository</c> で、送信先の表に書く。
/// </summary>
/// <remarks>
/// 外部へ送る処理は、ここには無い。端末の宛先を送信先の行に書くだけで、実際に送るのは MmmBatch。
/// このサーバーの DB のログインは、送る先の値の列 <c>Value</c> を読めない。登録した端末の一覧は、値を読まない要約で取る。
/// </remarks>
public sealed class NotificationsWebFeature : IWebFeature
{
    /// <inheritdoc />
    public Assembly ClientAssembly => typeof(NotificationsClient).Assembly;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        // 保存先は、要求ごとに作る。今のユーザー (送信先の使用者)が、要求ごとに違うため
        services.AddScoped<INotificationChannelRepository, SqlServerNotificationChannelRepository>();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, NotificationsJsonContext.Default));
    }

    /// <inheritdoc />
    public void MapEndpoints(RouteGroupBuilder api) => NotificationEndpoints.Map(api.MapGroup("/notifications"));
}
