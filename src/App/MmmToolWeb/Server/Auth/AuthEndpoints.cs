using System.Security.Claims;
using System.Text.Json;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MmmToolWeb.Auth.Core;
using MmmToolWeb.Auth.Core.Enrollment;
using MmmToolWeb.Auth.Core.Passkeys;
using MmmToolWeb.Client.Auth;
using MmmToolWeb.Server.Hosting;

namespace MmmToolWeb.Server.Auth;

/// <summary>
/// ログインに関する API (<c>/api/auth</c>)の登録。
/// </summary>
internal static class AuthEndpoints
{
    /// <summary>ログインに関する API の、道の先頭。</summary>
    public const string BasePath = "/api/auth";

    /// <summary>パスキーの登録の API の、道の先頭。</summary>
    public const string EnrollPath = BasePath + "/enroll";

    /// <summary>登録・ログインの口にかける、接続元ごとの回数制限の名前。</summary>
    public const string RateLimitPolicy = "auth";

    /// <summary>ログインした日時を入れる値の名前 (UTC の ISO 8601)。</summary>
    private const string SignedInAtClaim = "signed_in_at";

    /// <summary>ログイン状態を覚えておく日数。使うたびに延びる。</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(90);

    /// <summary>
    /// ログインの API を登録する。
    /// </summary>
    /// <param name="app">API を登録する先。</param>
    /// <returns>続けて呼ぶための <paramref name="app"/>。</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath);
        group.MapGet("/me", GetMe).RequireAuthorization();
        group.MapPost("/logout", Logout);

        var limited = group.MapGroup("").RequireRateLimiting(RateLimitPolicy);
        limited.MapPost("/enroll/start", EnrollStart);
        limited.MapPost("/enroll/finish", EnrollFinish);
        limited.MapPost("/login/start", LoginStart);
        limited.MapPost("/login/finish", LoginFinish);
        return app;
    }

    /// <summary>
    /// ログイン中のユーザーの情報を返す。
    /// </summary>
    /// <param name="user">ログイン中のユーザー。</param>
    /// <returns>ユーザーの情報。</returns>
    private static IResult GetMe(ClaimsPrincipal user)
    {
        var signedInAt = DateTimeOffset.TryParse(user.FindFirstValue(SignedInAtClaim), out var at) ? at : DateTimeOffset.UtcNow;
        return Results.Ok(new MeResponse(user.Identity?.Name ?? string.Empty, signedInAt));
    }

    /// <summary>
    /// ログイン状態を捨てる。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <returns>ログアウトの完了を表すタスク (応答は常に 204)。</returns>
    private static async Task Logout(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    /// <summary>
    /// 登録用コードを確かめて、パスキーの登録を始める。
    /// </summary>
    /// <param name="request">要求の内容。</param>
    /// <param name="codes">登録用コードの確認。</param>
    /// <param name="users">ログインできる人の検索。</param>
    /// <param name="passkeys">パスキーの手続き。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>ブラウザに渡す選択肢。コードが違う・持ち主が使えない状態のときは 403。</returns>
    private static async Task<IResult> EnrollStart(
        EnrollStartRequest request, EnrollmentCodeService codes, WebUserLookup users, PasskeyService passkeys,
        CancellationToken cancellationToken)
    {
        var ownerId = await codes.FindUserAsync(request.Code, cancellationToken);
        var owner = ownerId is null ? null : await users.FindActiveAsync(ownerId.Value, cancellationToken);
        if (owner is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var start = await passkeys.StartRegistrationAsync(
            owner.Id, owner.LoginName, owner.DisplayName, request.DeviceName, cancellationToken);
        return Results.Ok(ToResponse(start));
    }

    /// <summary>
    /// 登録用コードを使い終わりにして、パスキーの登録を完了し、ログインさせる。
    /// </summary>
    /// <param name="request">要求の内容。</param>
    /// <param name="context">今の要求。</param>
    /// <param name="codes">登録用コードの確認。</param>
    /// <param name="users">ログインできる人の検索。</param>
    /// <param name="passkeys">パスキーの手続き。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。コードが違うときは 403、検証に失敗したときは 400。</returns>
    private static async Task<IResult> EnrollFinish(
        EnrollFinishRequest request, HttpContext context, EnrollmentCodeService codes, WebUserLookup users,
        PasskeyService passkeys, CancellationToken cancellationToken)
    {
        var ownerId = await codes.ConsumeAsync(request.Code, cancellationToken);
        if (ownerId is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var response = request.Credential.Deserialize<AuthenticatorAttestationRawResponse>();
        if (response is null)
        {
            return Results.BadRequest();
        }
        int? appUserId;
        try
        {
            appUserId = await passkeys.FinishRegistrationAsync(request.CeremonyId, response, cancellationToken);
        }
        catch (Fido2VerificationException)
        {
            return Results.BadRequest();
        }
        // コードの持ち主と、パスキーを作る手続きを始めた人が、違うときは通さない
        if (appUserId is null || appUserId != ownerId)
        {
            return Results.BadRequest();
        }
        return await SignInAsync(context, users, appUserId.Value, cancellationToken);
    }

    /// <summary>
    /// パスキーでのログインを始める。
    /// </summary>
    /// <param name="passkeys">パスキーの手続き。</param>
    /// <returns>ブラウザに渡す選択肢。</returns>
    private static IResult LoginStart(PasskeyService passkeys) => Results.Ok(ToResponse(passkeys.StartLogin()));

    /// <summary>
    /// パスキーでのログインを完了して、ログインさせる。
    /// </summary>
    /// <param name="request">要求の内容。</param>
    /// <param name="context">今の要求。</param>
    /// <param name="users">ログインできる人の検索。</param>
    /// <param name="passkeys">パスキーの手続き。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。失敗なら 401。</returns>
    private static async Task<IResult> LoginFinish(
        LoginFinishRequest request, HttpContext context, WebUserLookup users, PasskeyService passkeys,
        CancellationToken cancellationToken)
    {
        var response = request.Credential.Deserialize<AuthenticatorAssertionRawResponse>();
        if (response is null)
        {
            return Results.BadRequest();
        }
        int? appUserId;
        try
        {
            appUserId = await passkeys.FinishLoginAsync(request.CeremonyId, response, cancellationToken);
        }
        catch (Fido2VerificationException)
        {
            return Results.Unauthorized();
        }
        if (appUserId is null)
        {
            return Results.Unauthorized();
        }
        return await SignInAsync(context, users, appUserId.Value, cancellationToken);
    }

    /// <summary>
    /// ログイン状態の Cookie を発行する。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <param name="users">ログインできる人の検索。</param>
    /// <param name="appUserId">ログインした人のユーザーの番号。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>発行できたら 204。その人が今は使えない状態 (削除済み・使える期間の外)なら 401。</returns>
    private static async Task<IResult> SignInAsync(
        HttpContext context, WebUserLookup users, int appUserId, CancellationToken cancellationToken)
    {
        var user = await users.FindActiveAsync(appUserId, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }
        var now = DateTimeOffset.UtcNow;
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(SignedInAtClaim, now.ToString("O")),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, IssuedUtc = now, ExpiresUtc = now + SessionLifetime });
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(SecurityLog.Category)
            .LogInformation("ログイン状態を発行しました。ユーザー番号={UserId} 接続元={Address}", user.Id, context.Connection.RemoteIpAddress);
        return Results.NoContent();
    }

    /// <summary>
    /// 手続きの開始の結果を、ブラウザに返す形にする。
    /// </summary>
    /// <param name="start">手続きの開始の結果。</param>
    /// <returns>ブラウザに返す内容。</returns>
    private static CeremonyStartResponse ToResponse(PasskeyStart start) =>
        new(start.CeremonyId, JsonDocument.Parse(start.OptionsJson).RootElement.Clone());
}
