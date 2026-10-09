using Microsoft.Extensions.DependencyInjection;

namespace MmmTool.Users.Core;

/// <summary>
/// ユーザー (<see cref="AppUser"/>)の部品を DI に登録する。
/// </summary>
public static class UsersServiceCollectionExtensions
{
    /// <summary>今のユーザー・ログインを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>ユーザーの保存先 (<see cref="IAppUserRepository"/>)と、ログイン名・パスワードを覚える先 (<see cref="ISavedCredentialStore"/>)は、DB の種類ごとの登録 (<c>AddSqlServerData</c> など)が行う。</remarks>
    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddSingleton<CurrentUser>();
        services.AddSingleton<UserSignInService>();
        services.AddSingleton<UserAccountService>();
        return services;
    }
}
