using System.Security.Claims;
using MmmTool.Users.Core;
using MmmToolWeb.Auth.Core;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// API (<c>/api</c>)の呼び出しごとに、ログインしている人が今も使える状態かを確かめ、その人を今のユーザーにする。
/// </summary>
/// <param name="next">次の処理。</param>
/// <remarks>
/// 削除された人・使える期間が終わった人は、ログインの Cookie が残っていても、ログインしていない扱い (401)にする。
/// 機能の保存先は、ここで決めた今のユーザー (<see cref="CurrentUser"/>)で、読み書きするノートを絞る。
/// </remarks>
internal sealed class ActiveUserMiddleware(RequestDelegate next)
{
    /// <summary>
    /// 要求を処理する。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <param name="users">ログインできる人の検索。</param>
    /// <param name="currentUser">今のユーザー。要求ごとに作られる。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task InvokeAsync(HttpContext context, WebUserLookup users, CurrentUser currentUser)
    {
        if (context.Request.Path.StartsWithSegments("/api") && context.User.Identity?.IsAuthenticated == true)
        {
            var found = int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? await users.FindActiveAsync(userId, context.RequestAborted)
                : null;
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
}
