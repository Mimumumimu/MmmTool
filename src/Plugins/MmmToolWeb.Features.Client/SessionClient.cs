using Microsoft.AspNetCore.Components;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// ログイン状態の操作であるログアウト。機能の画面から使う。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
/// <param name="navigation">画面の移動。</param>
public sealed class SessionClient(HttpClient http, NavigationManager navigation)
{
    /// <summary>
    /// ログアウトして、最初の画面 (ログイン)へ戻る。
    /// </summary>
    /// <returns>ログアウトの完了を表すタスク。</returns>
    public async Task LogoutAsync()
    {
        using var response = await http.PostAsync("api/auth/logout", content: null);
        navigation.NavigateTo("/", forceLoad: true);
    }
}
