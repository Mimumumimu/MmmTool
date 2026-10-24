namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// すべての応答に、ブラウザの守りの指定 (<c>Content-Security-Policy</c>)と、検索エンジンに載せない指定 (<c>X-Robots-Tag</c>)を付ける。
/// </summary>
/// <param name="next">次の処理。</param>
/// <remarks>
/// 検索エンジンに載せない指定は、サイトの存在を、検索で見つけられにくくするため。守りではなく、見つかりにくくするだけで、画面のファイルは、アドレスを知っていれば開ける。
/// 同じ指定を、<c>wwwroot/robots.txt</c> でも出す。
/// ほかの人が書いたノートの本文を、画面に出すので、整形の処理が万一破られても、本文の中のスクリプトが動かないための、2 重目の備え。
/// 自分のサイトのファイルと自分への通信だけを許し、スクリプトは自分のサイトのものと WebAssembly だけ、インラインのスクリプトと外の画像は許さない。
/// ページを、ほかのサイトの枠 (iframe)の中に出すことも許さない。
/// </remarks>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>検索エンジンに、載せない・たどらない・写しを残さない、と伝える指定。</summary>
    private const string RobotsPolicy = "noindex, nofollow, noarchive";

    /// <summary>付ける指定。</summary>
    private const string Policy =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data:; connect-src 'self'; "
        + "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>
    /// 要求を処理する。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.ContentSecurityPolicy = Policy;
        context.Response.Headers["X-Robots-Tag"] = RobotsPolicy;
        return next(context);
    }
}
