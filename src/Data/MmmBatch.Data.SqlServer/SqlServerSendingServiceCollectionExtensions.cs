using Microsoft.Extensions.DependencyInjection;
using MmmBatch.Data.SqlServer.Sending;
using MmmBatch.Sending.Core;
using MmmTool.Data.Connection;
using MmmTool.Data.SqlServer.Connection;

namespace MmmBatch.Data.SqlServer;

/// <summary>
/// MmmBatch が SQL Server を使うための部品を DI に登録する。
/// </summary>
public static class SqlServerSendingServiceCollectionExtensions
{
    /// <summary>データベース名・ユーザー名の初期値と、パスワードの保存名 (ログインは MmmTool と同じ。保存名は、アプリごとに分ける)</summary>
    public static DatabaseSettingsDefaults Defaults { get; } = new("MmmTool", "MmmTool", "MmmBatch.Database");

    /// <summary>SQL Server の接続と、送る対象・送信の状況の保存先を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// MmmTool のリマインダーなどの保存先 (<c>AddSqlServerData</c>)は登録しない (MmmBatch は、リマインダーを読むだけで、書かないため)。
    /// DB の接続の設定は、MmmBatch の設定ストアに持つ。ユーザーの特定 (ログイン)は、しない (MmmBatch はユーザーではない)。
    /// </remarks>
    public static IServiceCollection AddMmmBatchSqlServer(this IServiceCollection services)
    {
        services.AddDatabaseSettings(Defaults);
        services.AddSingleton<SqlServerConnectionFactoryBuilder>();
        services.AddSingleton<SqlServerDatabase>();
        services.AddSingleton<ISendTargetSource, SqlServerSendTargetSource>();
        services.AddSingleton<IReminderSendStatusRepository, SqlServerReminderSendStatusRepository>();
        return services;
    }
}
