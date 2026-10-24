using System.Reflection;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.NotificationChannels.Core;
using MmmToolWeb.Features;

namespace MmmToolWeb.Reminders;

/// <summary>
/// リマインダーの Web 版のサーバー側。「通知」の画面の一覧に、自分の送信先に届いたリマインダーを出すために、提供元を登録する。
/// </summary>
/// <remarks>
/// リマインダーの画面と API は持たない。リマインダーの Web 画面を作るときに、このプロジェクトに足す。docs/todo.md の「リマインダーの Web 画面を作る」を見る。
/// リマインダーを Web 版の「通知」に出さないときは、サーバー本体のこの機能の登録の 1 行を外す。
/// </remarks>
public sealed class RemindersWebFeature : IWebFeature
{
    /// <inheritdoc />
    /// <remarks>ブラウザ側の画面を持たないので、この機能のアセンブリを返す。<c>@page</c> は無い。</remarks>
    public Assembly ClientAssembly => typeof(RemindersWebFeature).Assembly;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        // 提供元は、要求ごとに作る。今のユーザーが、要求ごとに違うため
        services.AddScoped<IReceivedNotificationSource, SqlServerReceivedReminderSource>();
    }

    /// <inheritdoc />
    public void MapEndpoints(RouteGroupBuilder api)
    {
        // API は持たない
    }
}
