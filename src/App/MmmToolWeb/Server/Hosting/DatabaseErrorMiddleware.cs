using MmmSdk.Core.Components.Storage;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// DB の設定が足りない・接続できないときに、サーバーの内部エラー (500)ではなく、一時的に使えない (503)を返す。
/// </summary>
/// <param name="next">次の処理。</param>
/// <param name="logger">ログの出力先。</param>
internal sealed class DatabaseErrorMiddleware(RequestDelegate next, ILogger<DatabaseErrorMiddleware> logger)
{
    /// <summary>
    /// 要求を処理する。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DataFileException ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "DB を使えませんでした。");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }
}
