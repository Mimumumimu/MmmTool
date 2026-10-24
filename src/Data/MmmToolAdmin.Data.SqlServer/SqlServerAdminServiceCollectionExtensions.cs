using Microsoft.Extensions.DependencyInjection;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.CodeMaster;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Users.Core;
using MmmToolWeb.Auth.Core;
using MmmToolWeb.Auth.Core.Enrollment;
using MmmToolWeb.Data.SqlServer.Enrollment;

namespace MmmToolAdmin.Data.SqlServer;

/// <summary>
/// MmmToolAdmin が SQL Server を使うための部品を DI に登録する。
/// </summary>
public static class SqlServerAdminServiceCollectionExtensions
{
    /// <summary>データベース名・ユーザー名の初期値と、パスワードの保存名</summary>
    /// <remarks>ログインも保存名も MmmTool と同じ。MmmTool が資格情報マネージャーに保存したパスワードを、そのまま読む。</remarks>
    public static DatabaseSettingsDefaults Defaults { get; } = DatabaseSettingsDefaults.MmmTool;

    /// <summary>SQL Server の接続と、ユーザー・登録用コード・コードマスタの保存先を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// DB の接続の設定 (サーバー名など)は、MmmToolAdmin の設定ストアに持つ。パスワードだけは、MmmTool と同じ保存名なので、MmmTool で入れたものを使う。
    /// ユーザーの特定 (ログイン)は、しない。入口は、DB のログインだけ。
    /// </remarks>
    public static IServiceCollection AddMmmToolAdminSqlServer(this IServiceCollection services)
    {
        services.AddDatabaseSettings(Defaults);
        services.AddSingleton<SqlServerConnectionFactoryBuilder>();
        services.AddSingleton<SqlServerDatabase>();
        services.AddSingleton<IAppUserRepository, SqlServerAppUserRepository>();
        services.AddSingleton<ICodeMasterRepository, SqlServerCodeMasterRepository>();
        services.AddSingleton<IAppUserWebAuthRepository, SqlServerAppUserWebAuthRepository>();
        services.AddSingleton<WebUserLookup>();
        services.AddSingleton<EnrollmentCodeService>();
        return services;
    }
}
