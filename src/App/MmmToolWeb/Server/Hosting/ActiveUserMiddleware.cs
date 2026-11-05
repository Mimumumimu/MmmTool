using System.Security.Claims;
using MmmTool.Users.Core;
using MmmToolWeb.Auth.Core;
using MmmToolWeb.Auth.Core.Enrollment;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// API (<c>/api</c>)の呼び出しごとに、ログインしている人が今も使える状態かを確かめ、その人を今のユーザーにする。
/// </summary>
/// <param name="next">次の処理。</param>
/// <remarks>
/// 削除された人・使える期間が終わった人は、ログインの Cookie が残っていても、ログインしていない扱い (401)にする。
/// 管理者がログインの世代の番号を上げた人の、前の番号の Cookie も、同じ扱いにする。
/// 番号の確認は、ユーザーの確認とは別の表を読むので、API の呼び出しごとに、索引 1 回の読みが増える。ユーザーは共有の部品 (<c>MmmTool.Users.Core</c>)で読み、Web 版だけの表を、共有の部品に結合しないため。
/// 機能の保存先は、ここで決めた今のユーザー (<see cref="CurrentUser"/>)で、読み書きするノートを絞る。
/// </remarks>
internal sealed class ActiveUserMiddleware(RequestDelegate next)
{
    /// <summary>
    /// 要求を処理する。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <param name="users">ログインできる人の検索。</param>
    /// <param name="webAuth">ログインの世代の番号の保存先。</param>
    /// <param name="currentUser">今のユーザー。要求ごとに作られる。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task InvokeAsync(HttpContext context, WebUserLookup users, IAppUserWebAuthRepository webAuth, CurrentUser currentUser)
    {
        if (context.Request.Path.StartsWithSegments("/api") && context.User.Identity?.IsAuthenticated == true)
        {
            var found = int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? await users.FindActiveAsync(userId, context.RequestAborted)
                : null;
            if (found is not null && !await IsCurrentSessionAsync(context, webAuth, found.Id))
            {
                found = null;
            }
            if (found is null)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else
            {
                currentUser.Set(found);
            }
        }
        await next(context);
    }

    /// <summary>
    /// ログインの Cookie の世代の番号が、今の番号と同じかを返す。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <param name="webAuth">ログインの世代の番号の保存先。</param>
    /// <param name="userId">ログインしている人のユーザーの番号。</param>
    /// <returns>同じなら true。管理者が番号を上げたあとは、前の番号の Cookie は false。</returns>
    /// <remarks>番号を持たない Cookie は、番号 1 として比べる。番号を入れる前に発行した Cookie が、配ったとたんに無効にならないため。</remarks>
    private static async Task<bool> IsCurrentSessionAsync(HttpContext context, IAppUserWebAuthRepository webAuth, int userId)
    {
        var issued = int.TryParse(context.User.FindFirstValue(Auth.AuthEndpoints.SessionVersionClaim), out var version)
            ? version
            : Auth.AuthEndpoints.DefaultSessionVersion;
        return issued == await webAuth.GetSessionVersionAsync(userId, context.RequestAborted);
    }
}
