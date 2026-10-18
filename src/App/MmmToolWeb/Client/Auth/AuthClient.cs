using System.Net;
using System.Net.Http.Json;

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
public sealed class AuthClient(HttpClient http, PasskeyBrowser browser)
{
    /// <summary>
    /// ログイン中のユーザーを読む。
    /// </summary>
    /// <returns>ログイン中のユーザー。ログインしていないときは <c>null</c>。</returns>
    public async Task<MeResponse?> GetMeAsync()
    {
        using var response = await http.GetAsync("api/auth/me");
        return response.StatusCode == HttpStatusCode.Unauthorized
            ? null
            : await response.Content.ReadFromJsonAsync<MeResponse>();
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
            return AuthResult.Fail($"登録を始められませんでした ({(int)start.StatusCode})。");
        }
        var ceremony = await start.Content.ReadFromJsonAsync<CeremonyStartResponse>();
        var credential = await browser.CreateAsync(ceremony!.Options);
        if (credential is null)
        {
            return AuthResult.Fail("パスキーの登録を取りやめました。");
        }
        using var finish = await http.PostAsJsonAsync(
            "api/auth/enroll/finish", new EnrollFinishRequest(code, ceremony.CeremonyId, credential.Value));
        return finish.IsSuccessStatusCode
            ? AuthResult.Success
            : AuthResult.Fail($"登録を完了できませんでした ({(int)finish.StatusCode})。");
    }

    /// <summary>
    /// 登録済みのパスキーでログインする。
    /// </summary>
    /// <returns>手続きの結果。</returns>
    public async Task<AuthResult> LoginAsync()
    {
        using var start = await http.PostAsync("api/auth/login/start", content: null);
        if (!start.IsSuccessStatusCode)
        {
            return AuthResult.Fail($"ログインを始められませんでした ({(int)start.StatusCode})。");
        }
        var ceremony = await start.Content.ReadFromJsonAsync<CeremonyStartResponse>();
        var credential = await browser.GetAsync(ceremony!.Options);
        if (credential is null)
        {
            return AuthResult.Fail("ログインを取りやめました。");
        }
        using var finish = await http.PostAsJsonAsync(
            "api/auth/login/finish", new LoginFinishRequest(ceremony.CeremonyId, credential.Value));
        return finish.IsSuccessStatusCode
            ? AuthResult.Success
            : AuthResult.Fail($"ログインできませんでした ({(int)finish.StatusCode})。");
    }

    /// <summary>
    /// ログアウトする。
    /// </summary>
    /// <returns>ログアウトの完了を表すタスク。</returns>
    public async Task LogoutAsync()
    {
        using var response = await http.PostAsync("api/auth/logout", content: null);
    }
}
