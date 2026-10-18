namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// 探りを繰り返す接続元を、一定時間断る。探りは、存在しない道への要求と、画面の道ではない道に画面の入口を返しただけの要求。
/// </summary>
/// <param name="next">次の処理。</param>
/// <param name="guard">探りの数え方と、断った接続元の一覧。</param>
/// <param name="routes">画面の道の一覧。</param>
/// <param name="loggerFactory">ログの出力先を作る。</param>
internal sealed class ProbeGuardMiddleware(RequestDelegate next, ProbeGuard guard, ClientRoutes routes, ILoggerFactory loggerFactory)
{
    /// <summary>画面の入口を返す仕組み (<c>MapFallbackToFile</c>)の道の型。</summary>
    private const string FallbackPattern = "{*path:nonfile}";

    /// <summary>普通に使っても <c>404</c> になる道。探りに数えない。</summary>
    private static readonly HashSet<string> NormalNotFoundPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/favicon.ico",
        "/apple-touch-icon-precomposed.png",
        "/MmmToolWeb.Client.styles.css",
    };

    /// <summary>記録の書き手。</summary>
    private readonly ILogger _logger = loggerFactory.CreateLogger(SecurityLog.Category);

    /// <summary>
    /// 断っている接続元なら断り、そうでなければ処理して、探りなら数える。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var address = ClientAddress.PartitionKey(context);
        var path = context.Request.Path.Value ?? string.Empty;
        if (guard.IsBlocked(address, DateTimeOffset.UtcNow))
        {
            guard.RecordRejection(address, path, DateTimeOffset.UtcNow);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        await next(context);

        if (IsProbe(context) && guard.RecordProbe(address, path, DateTimeOffset.UtcNow))
        {
            _logger.LogWarning(
                "探りが続いたので、{Minutes} 分のあいだ断ります。接続元={Address} 最後の道={Path}",
                (int)ProbeGuard.BlockDuration.TotalMinutes, address, ProbeGuard.Sanitize(path));
        }
    }

    /// <summary>
    /// 処理した要求が、探りかを返す。
    /// </summary>
    /// <param name="context">処理を終えた要求。</param>
    /// <returns>探りなら true。</returns>
    private bool IsProbe(HttpContext context)
    {
        var path = context.Request.Path;
        var endpoint = context.GetEndpoint();
        if (context.Response.StatusCode == StatusCodes.Status404NotFound)
        {
            // 経路が決まっているものは、API が「ノートが無い」と返した 404 で、探りではない
            return endpoint is null && !NormalNotFoundPaths.Contains(path.Value ?? string.Empty);
        }
        return endpoint is RouteEndpoint { RoutePattern.RawText: FallbackPattern } && !routes.IsKnown(path);
    }
}
