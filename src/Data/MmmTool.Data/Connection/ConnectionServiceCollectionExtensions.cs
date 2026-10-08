using Microsoft.Extensions.DependencyInjection;

namespace MmmTool.Data.Connection;

/// <summary>
/// DB への接続の設定を DI に登録する。
/// </summary>
public static class ConnectionServiceCollectionExtensions
{
    /// <summary>接続の設定の読み書き (<see cref="DatabaseSettingsService"/>)を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>汎用設定ストア (<c>ISettingsStore</c>)と秘密の保管庫 (<c>ISecretStore</c>)は、SDK の登録が行う。</remarks>
    public static IServiceCollection AddDatabaseSettings(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseSettingsService>();
        return services;
    }
}
