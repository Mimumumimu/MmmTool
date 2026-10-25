using System.Reflection;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.SqlServer.WorkItems;
using MmmTool.DayContent.Core;
using MmmTool.WorkItems.Core;
using MmmToolWeb.Features;

namespace MmmToolWeb.WorkItems;

/// <summary>
/// 作業管理の Web 版のサーバー側。ノートの画面に、作業報告の内容を出すために、内容の提供元を登録する。
/// </summary>
/// <remarks>
/// 作業管理の画面と API は持たない。読みは、MmmTool と同じ <see cref="WorkReportDayContentSource"/> と <c>SqlServerWorkItemRepository</c> を使う。
/// 作業管理を Web 版に出さないときは、サーバー本体のこの機能の登録の 1 行を外す。
/// </remarks>
public sealed class WorkItemsWebFeature : IWebFeature
{
    /// <inheritdoc />
    /// <remarks>ブラウザ側の画面を持たないので、この機能のアセンブリを返す。<c>@page</c> は無い。</remarks>
    public Assembly ClientAssembly => typeof(WorkItemsWebFeature).Assembly;

    /// <inheritdoc />
    public int? FeatureNo => CodeFeatures.WorkItems;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        // 保存先と提供元は、要求ごとに作る。今のユーザーが、要求ごとに違うため
        services.AddScoped<IWorkItemRepository, SqlServerWorkItemRepository>();
        services.AddScoped<IDayContentSource, WorkReportDayContentSource>();
    }

    /// <inheritdoc />
    public void MapEndpoints(RouteGroupBuilder api)
    {
        // API は持たない
    }
}
