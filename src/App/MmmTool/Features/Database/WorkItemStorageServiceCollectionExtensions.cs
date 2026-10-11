using Microsoft.Extensions.DependencyInjection;
using MmmTool.Data.Connection;
using MmmTool.Data.Sqlite.WorkItems;
using MmmTool.Data.SqlServer.WorkItems;
using MmmTool.WorkItems.Core;

namespace MmmTool.Features.Database;

/// <summary>
/// 作業管理の保存先 (ローカルの SQLite / DB の SQL Server)を、設定で選んで DI に登録する。
/// </summary>
public static class WorkItemStorageServiceCollectionExtensions
{
    /// <summary>作業管理の保存先を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 設定 (<see cref="DatabaseMode"/>)が DB のときは SQL Server、それ以外は SQLite (<c>Data/MmmTool.db</c>)。
    /// 選ぶのは、SQLite と SQL Server の両方を参照するホストの役目 (MmmBatch は SQLite を参照しないので、<c>MmmTool.Data.SqlServer</c> の側には置かない)。
    /// <c>AddSqliteData</c> と <c>AddSqlServerData</c> のあとに呼ぶ。
    /// </remarks>
    public static IServiceCollection AddWorkItemStorage(this IServiceCollection services)
    {
        services.AddSingleton<IWorkItemRepository>(provider =>
            provider.GetRequiredService<DatabaseSettingsService>().Load().Mode == DatabaseMode.SqlServer
                ? provider.GetRequiredService<SqlServerWorkItemRepository>()
                : provider.GetRequiredService<SqliteWorkItemRepository>());
        return services;
    }
}
