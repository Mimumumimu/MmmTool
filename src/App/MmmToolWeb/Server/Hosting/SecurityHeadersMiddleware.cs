namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// すべての応答に、ブラウザの守りの指定 (<c>Content-Security-Policy</c>)を付ける。
/// </summary>
/// <param name="next">次の処理。</param>
/// <remarks>
/// ほかの人が書いたノートの本文を、画面に出すので、整形の処理が万一破られても、本文の中のスクリプトが動かないための、2 重目の備え。
/// 自分のサイトのファイルと自分への通信だけを許し、スクリプトは自分のサイトのものと WebAssembly だけ、インラインのスクリプトと外の画像は許さない。
/// ページを、ほかのサイトの枠 (iframe)の中に出すことも許さない。
/// </remarks>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
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
        return next(context);
    }
}
