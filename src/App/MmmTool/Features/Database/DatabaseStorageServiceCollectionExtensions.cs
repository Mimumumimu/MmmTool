using Microsoft.Extensions.DependencyInjection;
using MmmTool.Data.Connection;

namespace MmmTool.Features.Database;

/// <summary>
/// 保存先 (ローカルの SQLite / DB の SQL Server)を、設定で選んで DI に登録する共通の部品。
/// </summary>
public static class DatabaseStorageServiceCollectionExtensions
{
    /// <summary>機能の保存先を、設定で選んで登録する</summary>
    /// <typeparam name="TService">機能が使う保存先のインターフェース</typeparam>
    /// <typeparam name="TLocal">ローカルモードの保存先 (SQLite。<c>AddSqliteData</c> が登録したもの)</typeparam>
    /// <typeparam name="TServer">DB モードの保存先 (SQL Server。<c>AddSqlServerData</c> が登録したもの)</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 設定 (<see cref="DatabaseMode"/>)が DB のときは <typeparamref name="TServer"/>、それ以外は <typeparamref name="TLocal"/>。
    /// 選ぶのは、SQLite と SQL Server の両方を参照するホストの役目 (MmmBatch は SQLite を参照しないので、<c>MmmTool.Data.SqlServer</c> の側には置かない)。
    /// <c>AddSqliteData</c> と <c>AddSqlServerData</c> のあとに呼ぶ。機能が増えても、選ぶ処理はここ 1 か所で、機能は 1 行を足すだけ。
    /// </remarks>
    public static IServiceCollection AddStorage<TService, TLocal, TServer>(this IServiceCollection services)
        where TService : class
        where TLocal : class, TService
        where TServer : class, TService
        => services.AddSingleton<TService>(provider =>
            provider.GetRequiredService<DatabaseSettingsService>().Load().Mode == DatabaseMode.SqlServer
                ? provider.GetRequiredService<TServer>()
                : provider.GetRequiredService<TLocal>());
}
