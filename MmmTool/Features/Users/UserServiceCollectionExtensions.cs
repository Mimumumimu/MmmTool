using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmTool.Features.Users.Registration;

namespace MmmTool.Features.Users;

/// <summary>
/// ユーザーの登録 (DB モードのとき、この PC を登録する)を DI に登録する。
/// </summary>
public static class UserServiceCollectionExtensions
{
    /// <summary>ユーザーの特定の準備と、登録の画面を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 起動時の準備は、登録した順に動くので、各機能の登録より前に呼ぶ (機能の準備より先に、今のユーザーを決めておく)。
    /// ユーザーの部品 (<c>AddUsers</c>)と、保存先は、DB の種類ごとの登録 (<c>AddSqlServerData</c>)が行う。
    /// </remarks>
    public static IServiceCollection AddUserRegistration(this IServiceCollection services)
    {
        services.AddSingleton<UserRegistrationService>();
        services.AddStartupTask<UserStartup>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<UserRegistrationWindow>();
        services.AddTransient<UserRegistrationViewModel>();
        return services;
    }
}
