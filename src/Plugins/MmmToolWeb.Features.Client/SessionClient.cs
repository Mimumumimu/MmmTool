using Microsoft.AspNetCore.Components;

namespace MmmToolWeb.Features.Client;

/// <summary>ログイン状態の操作であるログアウトとログインが切れたときの戻り</summary>
/// <param name="http">サーバーへの接続</param>
/// <param name="navigation">画面の移動</param>
/// <param name="browser">
/// ブラウザの部品
/// 端末の控えを捨てるために使う
/// </param>
/// <remarks>機能の画面と API を呼ぶ部品から使う</remarks>
public sealed class SessionClient(HttpClient http, NavigationManager navigation, BrowserClient browser)
{
    /// <summary>ログアウトして最初の画面へ戻る</summary>
    /// <returns>ログアウトの完了を表すタスク</returns>
    /// <remarks>
    /// 端末の控え (メッセージなど)も捨てる。
    /// 共有の端末で、前の人のメッセージが見えないようにするため。
    /// </remarks>
    public async Task LogoutAsync()
    {
        await browser.ClearCacheAsync();
        using var response = await http.PostAsync("api/auth/logout", content: null);
        GoToSignIn();
    }

    /// <summary>ログインの画面へ全体を読み直して戻る</summary>
    /// <remarks>
    /// API が 401 を返したときに <see cref="ApiCaller"/> が呼ぶ。
    /// 画面は移動を書かず、<see cref="ApiFailure.SignedOut"/> のとき、失敗の文を出さずに処理を終える。
    /// 全体を読み直すのは読み込み済みの内容をログインし直したあとの人に見せないため。
    /// </remarks>
    public void GoToSignIn() => navigation.NavigateTo("/", forceLoad: true);
}
