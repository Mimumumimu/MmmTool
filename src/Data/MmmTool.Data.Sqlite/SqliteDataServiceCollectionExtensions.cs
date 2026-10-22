using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.Data.Sqlite.Notes;
using MmmTool.Data.Sqlite.WorkItems;

namespace MmmTool.Data.Sqlite;

/// <summary>
/// ローカルの SQLite に保存するための部品を DI に登録する。
/// </summary>
public static class SqliteDataServiceCollectionExtensions
{
    /// <summary>データベースのファイル名 (データのフォルダーの中)</summary>
    public const string FileName = "MmmTool.db";

    /// <summary>SQLite の部品を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// データのフォルダー (<see cref="AppEnvironment.DataDirectory"/>)の <c>MmmTool.db</c> を使う。ファイルは最初に使うときに作り、版を上げたあと、あるべき形 (<c>Sql/schema.sql</c>)と比べて、足りない列・表・索引を足す。
    /// 保存先の選択 (ローカル / DB)は、ホストが設定で行う (<c>IWorkItemRepository</c> は、ここでは登録しない)。
    /// </remarks>
    public static IServiceCollection AddSqliteData(this IServiceCollection services)
    {
        services.AddSingleton(provider => new SqliteDatabase(new SqliteDatabaseOptions
        {
            FilePath = Path.Combine(provider.GetRequiredService<AppEnvironment>().DataDirectory, FileName),
            Migrations = SqliteMigrations.Load(),
            SchemaSql = SqliteMigrations.LoadSchema(),
        }));
        services.AddSingleton<SqliteWorkItemRepository>();
        services.AddSingleton<SqliteNoteRepository>();
        return services;
    }
}
