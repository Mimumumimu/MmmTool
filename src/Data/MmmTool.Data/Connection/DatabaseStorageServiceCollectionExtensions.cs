using Microsoft.Extensions.DependencyInjection;

namespace MmmTool.Data.Connection;

/// <summary>保存先を設定で選んで DI に登録する共通の部品</summary>
public static class DatabaseStorageServiceCollectionExtensions
{
    /// <summary>機能の保存先を設定で選んで登録する</summary>
    /// <typeparam name="TService">機能が使う保存先のインターフェース</typeparam>
    /// <typeparam name="TLocal">
    /// ローカルモードの保存先。
    /// 機能が登録したもの (JSON や「使えない」保存先)か、<c>AddSqliteData</c> が登録した SQLite の保存先。
    /// </typeparam>
    /// <typeparam name="TServer">
    /// DB モードの保存先で、SQL Server。
    /// <c>AddSqlServerData</c> が登録したもの。
    /// </typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 設定 (<see cref="DatabaseMode"/>)が DB のときは <typeparamref name="TServer"/>、それ以外は <typeparamref name="TLocal"/>。
    /// 選ぶ処理はここ 1 か所で、機能は 1 行を足すだけ。
    /// SQLite の型を参照しないので、MmmBatch も参照する <c>MmmTool.Data</c> に置く。
    /// 保存先は最初に使うときに選ぶので、設定が無い初回の選択画面はその前に出せる。
    /// <typeparamref name="TLocal"/> と <typeparamref name="TServer"/> を登録したあとに呼ぶ。
    /// 同じ <typeparamref name="TService"/> を登録済みなら、あとの登録が前の登録を置き換える。
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
