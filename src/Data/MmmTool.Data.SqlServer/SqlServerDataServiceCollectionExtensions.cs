using Microsoft.Extensions.DependencyInjection;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Data.Users;
using MmmTool.Reminders.Core;
using MmmTool.Reminders.Core.Json;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer;

/// <summary>
/// SQL Server に保存するための部品を DI に登録する。
/// </summary>
public static class SqlServerDataServiceCollectionExtensions
{
    /// <summary>SQL Server の部品を登録し、保存先の選択 (ローカル / DB)を設定どおりにする</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 機能が登録した JSON の保存先 (<see cref="JsonReminderRepository"/>)を、設定 (<see cref="DatabaseMode"/>)が DB のときだけ、SQL Server の保存先に置き換える。
    /// 保存先は最初に使うときに選ぶので、設定が無い初回の選択画面は、その前に出せる。DB に接続するのは、DB を選んだときに、最初に読み書きするとき。
    /// 機能 (<c>AddFeaturePlugin</c>)を登録したあとに呼ぶ (あとの登録が、前の登録を置き換えるため)。
    /// </remarks>
    public static IServiceCollection AddSqlServerData(this IServiceCollection services)
    {
        services.AddUsers();
        services.AddDatabaseSettings();

        services.AddSingleton<SqlServerConnectionFactoryBuilder>();
        services.AddSingleton<SqlServerDatabase>();
        services.AddSingleton<IAppUserRepository, SqlServerAppUserRepository>();
        services.AddSingleton<ISavedCredentialStore, SecretSavedCredentialStore>();
        services.AddSingleton<SqlServerReminderRepository>();

        services.AddSingleton<IReminderRepository>(provider =>
            provider.GetRequiredService<DatabaseSettingsService>().Load().Mode == DatabaseMode.SqlServer
                ? provider.GetRequiredService<SqlServerReminderRepository>()
                : provider.GetRequiredService<JsonReminderRepository>());
        return services;
    }
}
