using System.Reflection;
using MmmTool.Data.SqlServer.Messages;
using MmmTool.Messages.Core;
using MmmToolWeb.Features;
using MmmToolWeb.Messages.Client;

namespace MmmToolWeb.Messages;

/// <summary>
/// メッセージ送信の Web 版のサーバー側。宛先の読み出しと依頼の保存は、MmmTool と同じ <c>SqlServerMessageSendRequestRepository</c> を使う。
/// </summary>
/// <remarks>外部へ送る処理は、ここには無い。依頼の行を DB に書くだけで、実際に送るのは MmmBatch。</remarks>
public sealed class MessagesWebFeature : IWebFeature
{
    /// <inheritdoc />
    public Assembly ClientAssembly => typeof(MessagesClient).Assembly;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        // 保存先は、要求ごとに作る。今のユーザー (送った人)が、要求ごとに違うため
        services.AddScoped<IMessageSendRequestRepository, SqlServerMessageSendRequestRepository>();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, MessagesJsonContext.Default));
    }

    /// <inheritdoc />
    public void MapEndpoints(RouteGroupBuilder api) => MessageEndpoints.Map(api.MapGroup("/messages"));
}
