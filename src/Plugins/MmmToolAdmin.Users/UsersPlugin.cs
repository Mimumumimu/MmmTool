using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;

namespace MmmToolAdmin.Users;

/// <summary>ユーザーの入口で、ページを登録する</summary>
/// <remarks>
/// ユーザーと登録用コードの保存先は、ホストが DB の実装を登録する。
/// 接続の設定画面と、それを開く口は、ホストが <c>AddConnectionSettingsDialog</c> で登録する。
/// </remarks>
public sealed class UsersPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // MmmToolAdmin の機能は、オフにできず、オン・オフの画面も持たない。キーは、サイドバーの並びのために使う
        services.AddAlwaysOnFeature(UsersFeature.Key, UsersFeature.DisplayName);

        // ページは、作ったあと使い回す。ページごとのスコープから作るので、ページを捨てるときに、画面と ViewModel の購読もまとめて解放される
        services.AddTransient<UsersViewModel>();
        // アイコンは Segoe Fluent Icons の People で、E716
        services.AddNavigationPage<UsersPage>(UsersFeature.DisplayName, "", NavigationArea.Top, UsersFeature.Key);
    }
}
