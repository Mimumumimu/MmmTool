using Microsoft.Extensions.DependencyInjection;

namespace MmmTool.Data.SqlServer.WinUI.Connection;

/// <summary>DB の接続の設定画面を DI に登録する</summary>
public static class ConnectionSettingsServiceCollectionExtensions
{
    /// <summary>接続の設定画面と、それを開く口を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>閉じたウィンドウは再表示できないので、画面と ViewModel は、開くたびに作る Transient にする。</remarks>
    public static IServiceCollection AddConnectionSettingsDialog(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionSettingsDialogService, ConnectionSettingsDialogService>();
        services.AddTransient<DatabaseConnectionViewModel>();
        services.AddTransient<ConnectionSettingsViewModel>();
        services.AddTransient<ConnectionSettingsWindow>();
        return services;
    }
}
