using Microsoft.AspNetCore.Components;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// ログイン状態の操作であるログアウト。機能の画面から使う。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
/// <param name="navigation">画面の移動。</param>
/// <param name="browser">ブラウザの部品。端末の控えを捨てるために使う。</param>
public sealed class SessionClient(HttpClient http, NavigationManager navigation, BrowserClient browser)
{
    /// <summary>
    /// ログアウトして、最初の画面 (ログイン)へ戻る。
    /// </summary>
    /// <returns>ログアウトの完了を表すタスク。</returns>
    /// <remarks>端末の控え (メッセージなど)も捨てる。共有の端末で、前の人のメッセージが見えないようにするため。</remarks>
    public async Task LogoutAsync()
    {
        await browser.ClearCacheAsync();
        using var response = await http.PostAsync("api/auth/logout", content: null);
        navigation.NavigateTo("/", forceLoad: true);
    }
}
