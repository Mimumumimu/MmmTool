using Microsoft.Extensions.DependencyInjection;

namespace MmmTool.Data.Connection;

/// <summary>
/// DB への接続の設定を DI に登録する。
/// </summary>
public static class ConnectionServiceCollectionExtensions
{
    /// <summary>接続の設定の読み書き (<see cref="DatabaseSettingsService"/>)を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="defaults">アプリごとの初期値。渡さなければ MmmTool の既定</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>アプリごとの初期値 (<see cref="DatabaseSettingsDefaults"/>)は、渡さなければ MmmTool の既定。汎用設定ストア (<c>ISettingsStore</c>)と秘密の保管庫 (<c>ISecretStore</c>)は、SDK の登録が行う。</remarks>
    public static IServiceCollection AddDatabaseSettings(this IServiceCollection services, DatabaseSettingsDefaults? defaults = null)
    {
        services.AddSingleton(defaults ?? DatabaseSettingsDefaults.MmmTool);
        services.AddSingleton<DatabaseSettingsService>();
        return services;
    }
}
