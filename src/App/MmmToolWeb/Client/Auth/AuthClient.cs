using System.Net;
using System.Net.Http.Json;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Client.Auth;

/// <summary>
/// ログインの手続きの結果。
/// </summary>
/// <param name="Succeeded">成功したか。</param>
/// <param name="Message">失敗したときに画面に出す文。</param>
public sealed record AuthResult(bool Succeeded, string? Message = null)
{
    /// <summary>成功。</summary>
    public static AuthResult Success { get; } = new(true);

    /// <summary>
    /// 失敗を作る。
    /// </summary>
    /// <param name="message">画面に出す文。</param>
    /// <returns>失敗の結果。</returns>
    public static AuthResult Fail(string message) => new(false, message);
}

/// <summary>
/// サーバーのログインの API と、ブラウザのパスキーをつなぐ。
/// </summary>
/// <param name="http">サーバーへの接続。</param>
/// <param name="browser">ブラウザのパスキーの呼び出し。</param>
/// <param name="browserClient">ブラウザの部品。ログインできたとき、端末の控えを捨てるために使う。</param>
public sealed class AuthClient(HttpClient http, PasskeyBrowser browser, BrowserClient browserClient)
{
    /// <summary>
    /// ログイン中のユーザーを読む。
    /// </summary>
    /// <returns>ログイン中のユーザー。ログインしていないときは <c>null</c>。</returns>
    public async Task<MeResponse?> GetMeAsync()
    {
        using var response = await http.GetAsync("api/auth/me");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MeResponse>() : null;
    }

    /// <summary>
    /// 登録用コードを使って、この端末にパスキーを登録し、ログインする。
    /// </summary>
    /// <param name="code">管理者が出した登録用コード。</param>
    /// <returns>手続きの結果。</returns>
    public async Task<AuthResult> EnrollAsync(string code)
    {
        using var start = await http.PostAsJsonAsync("api/auth/enroll/start", new EnrollStartRequest(code, await browser.DeviceNameAsync()));
        if (start.StatusCode == HttpStatusCode.Forbidden)
        {
            return AuthResult.Fail("登録用コードが違うか、期限が切れています。");
        }
        if (!start.IsSuccessStatusCode)
        {
            return AuthResult.Fail($"登録を始められませんでした。エラー番号は {(int)start.StatusCode} です。");
        }
        var ceremony = await start.Content.ReadFromJsonAsync<CeremonyStartResponse>();
        var credential = await browser.CreateAsync(ceremony!.Options);
        if (credential is null)
        {
            return AuthResult.Fail("パスキーの登録を取りやめました。");
        }
        using var finish = await http.PostAsJsonAsync(
            "api/auth/enroll/finish", new EnrollFinishRequest(code, ceremony.CeremonyId, credential.Value));
        if (!finish.IsSuccessStatusCode)
        {
            return AuthResult.Fail($"登録を完了できませんでした。エラー番号は {(int)finish.StatusCode} です。");
        }

        await ClearCacheAsync();
        return AuthResult.Success;
    }

#if DEBUG
    /// <summary>
    /// 開発用のビルド (Debug)だけ、ログイン名を指定して、パスワードなしでログインする。
    /// </summary>
    /// <param name="loginName">ログインする人のログイン名。</param>
    /// <returns>手続きの結果。</returns>
    public async Task<AuthResult> DevLoginAsync(string loginName)
    {
        using var response = await http.PostAsJsonAsync("api/auth/dev-login", new DevLoginRequest(loginName));
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            await ClearCacheAsync();
            return AuthResult.Success;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => AuthResult.Fail("今使えるユーザーが見つかりません。"),
            _ => AuthResult.Fail($"ログインできませんでした。エラー番号は {(int)response.StatusCode} です。"),
        };
    }

#endif
    /// <summary>
    /// 登録済みのパスキーでログインする。
    /// </summary>
    /// <returns>手続きの結果。</returns>
    public async Task<AuthResult> LoginAsync()
    {
        using var start = await http.PostAsync("api/auth/login/start", content: null);
        if (!start.IsSuccessStatusCode)
        {
            return AuthResult.Fail($"ログインを始められませんでした。エラー番号は {(int)start.StatusCode} です。");
        }
        var ceremony = await start.Content.ReadFromJsonAsync<CeremonyStartResponse>();
        var credential = await browser.GetAsync(ceremony!.Options);
        if (credential is null)
        {
            return AuthResult.Fail("ログインを取りやめました。");
        }
        using var finish = await http.PostAsJsonAsync(
            "api/auth/login/finish", new LoginFinishRequest(ceremony.CeremonyId, credential.Value));
        if (!finish.IsSuccessStatusCode)
        {
            return AuthResult.Fail($"ログインできませんでした。エラー番号は {(int)finish.StatusCode} です。");
        }

        await ClearCacheAsync();
        return AuthResult.Success;
    }

    /// <summary>
    /// ログインできたとき、この端末の控え (メッセージなど)を捨てる。
    /// </summary>
    /// <returns>捨てたことを表すタスク。</returns>
    /// <remarks>共有の端末で、前の人がログアウトせずに終えていても、前の人のメッセージが見えないようにするため。控えは、いつでも作り直せる。</remarks>
    private Task ClearCacheAsync() => browserClient.ClearCacheAsync();
}
