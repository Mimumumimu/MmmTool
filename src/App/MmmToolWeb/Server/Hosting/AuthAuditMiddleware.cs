namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// 登録・ログインの API (<c>/api/auth</c>)が失敗や拒否で終わったときに、守りの記録を残す。
/// </summary>
/// <param name="next">次の処理。</param>
/// <param name="loggerFactory">ログの出力先を作る。</param>
/// <remarks>
/// 回数制限の拒否 429 は、ここでは記録しない。連打されると、記録が際限なく増えるため。アクセスログには残る。
/// ログイン中かの確認 <c>/api/auth/me</c> の 401 は、ログインしていないだけなので記録しない。
/// </remarks>
internal sealed class AuthAuditMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    /// <summary>記録の書き手。</summary>
    private readonly ILogger _logger = loggerFactory.CreateLogger(SecurityLog.Category);

    /// <summary>
    /// 要求を処理して、失敗で終わったら記録する。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        var status = context.Response.StatusCode;
        // 経路が決まった要求だけ記録する (存在しない道への探りは、アクセスログに残る)
        if (status < StatusCodes.Status400BadRequest || status == StatusCodes.Status429TooManyRequests
            || context.GetEndpoint() is null
            || !context.Request.Path.StartsWithSegments(Auth.AuthEndpoints.BasePath)
            || context.Request.Path.StartsWithSegments(Auth.AuthEndpoints.BasePath + "/me"))
        {
            return;
        }
        _logger.LogWarning(
            "{Method} {Path} は {Status} で終わりました。接続元={Address}",
            context.Request.Method, context.Request.Path.Value, status, context.Connection.RemoteIpAddress);
    }
}
