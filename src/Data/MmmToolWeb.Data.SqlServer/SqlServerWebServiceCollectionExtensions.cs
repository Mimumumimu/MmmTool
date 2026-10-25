using Microsoft.Extensions.DependencyInjection;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Users.Core;
using MmmToolWeb.Auth.Core.Enrollment;
using MmmToolWeb.Auth.Core.Passkeys;
using MmmToolWeb.Data.SqlServer.Enrollment;
using MmmToolWeb.Data.SqlServer.Passkeys;

namespace MmmToolWeb.Data.SqlServer;

/// <summary>
/// MmmToolWeb が SQL Server を使うための部品を DI に登録する。
/// </summary>
public static class SqlServerWebServiceCollectionExtensions
{
    /// <summary>データベース名・ユーザー名の初期値と、パスワードの保存名 (ログインは Web 版専用の <c>MmmToolWeb</c>。保存名は、アプリごとに分ける)</summary>
    public static DatabaseSettingsDefaults Defaults { get; } = new("MmmTool", "MmmToolWeb", "MmmToolWeb.Database");

    /// <summary>SQL Server の接続と、ユーザー・パスキー・登録用コードの保存先を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// MmmTool のノート・リマインダーなどの保存先 (<c>AddSqlServerData</c>)は、ここでは登録しない (機能の画面を足すとき、その機能が登録する)。
    /// ユーザーは読むだけで、パスワードのハッシュは読まない (専用のログインに、その列の権限を与えない)。
    /// </remarks>
    public static IServiceCollection AddMmmToolWebSqlServer(this IServiceCollection services)
    {
        services.AddDatabaseSettings(Defaults);
        services.AddSingleton<SqlServerConnectionFactoryBuilder>();
        services.AddSingleton<SqlServerDatabase>();
        services.AddSingleton<IAppUserRepository, SqlServerAppUserRepository>();
        services.AddSingleton<IAppUserAdminSettingRepository, SqlServerAppUserAdminSettingRepository>();
        services.AddSingleton<FeatureAccessService>();
        services.AddSingleton<IAppUserPasskeyRepository, SqlServerAppUserPasskeyRepository>();
        services.AddSingleton<IAppUserWebAuthRepository, SqlServerAppUserWebAuthRepository>();
        return services;
    }
}
