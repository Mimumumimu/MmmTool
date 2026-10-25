#if DEBUG
using MmmToolWeb.Auth.Core;
using MmmToolWeb.Client.Auth;

namespace MmmToolWeb.Server.Auth;

/// <summary>
/// 開発用のビルド (Debug)だけにある、パスワードなしのログインの API の登録。
/// </summary>
/// <remarks>
/// <c>#if DEBUG</c> で囲むので、Release で発行した物には、この処理が入らない。
/// Funnel も同じ PC の localhost から転送してくるので、「localhost からの要求だけ」は条件に使わない。
/// 代わりに、Funnel が付ける転送元の見出しがある要求を断る。
/// </remarks>
internal static class DevAuthEndpoints
{
    /// <summary>パスワードなしのログインの API の道。</summary>
    public const string LoginPath = AuthEndpoints.BasePath + "/dev-login";

    /// <summary>
    /// パスワードなしのログインの API を登録する。環境が <c>Development</c> のときだけ登録する。
    /// </summary>
    /// <param name="app">API を登録する先。</param>
    /// <returns>続けて呼ぶための <paramref name="app"/>。</returns>
    public static WebApplication MapDevAuthEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning("開発用: ログイン名だけでログインできる口 ({Path}) を開いています。", LoginPath);
            app.MapPost(LoginPath, DevLogin);
        }
        return app;
    }

    /// <summary>
    /// ログイン名の人として、パスワードなしでログインさせる。
    /// </summary>
    /// <param name="request">要求の内容。</param>
    /// <param name="context">今の要求。</param>
    /// <param name="users">ログインできる人の検索。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。転送元の見出しがある要求は 404。使えるユーザーが無ければ 401。</returns>
    private static async Task<IResult> DevLogin(
        DevLoginRequest request, HttpContext context, WebUserLookup users, CancellationToken cancellationToken)
    {
        // UseForwardedHeaders が X-Forwarded-For を X-Original-For へ移すので、両方を見る
        if (context.Request.Headers.ContainsKey("X-Forwarded-For") || context.Request.Headers.ContainsKey("X-Original-For"))
        {
            return Results.NotFound();
        }
        var user = await users.FindActiveByLoginNameAsync(request.LoginName.Trim(), cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }
        return await AuthEndpoints.IssueSessionAsync(context, users, user.Id, cancellationToken) is null
            ? Results.Unauthorized()
            : Results.NoContent();
    }
}
#endif
