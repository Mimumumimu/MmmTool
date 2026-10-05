namespace MmmTool.Core.Backlog;

/// <summary>
/// Backlog のファイル画面の URL を、場所 (<see cref="BacklogLocation"/>)に分ける。
/// </summary>
/// <remarks>
/// URL の形は <c>https://{domain}/file/{projectKey}/{エンコード済みフォルダーパス}</c>。
/// API キーを送る先なので、Backlog のドメイン以外は受け付けない (違う URL を貼ったときに、キーを別のサーバーへ送らないため)。
/// </remarks>
public static class BacklogLocationParser
{
    /// <summary>Backlog のドメイン (これらのサブドメインだけを受け付ける)</summary>
    private static readonly string[] BacklogDomains = ["backlog.jp", "backlog.com", "backlogtool.com"];

    /// <summary>URL を場所に分ける</summary>
    /// <param name="url">入力された URL</param>
    /// <returns>分けた場所。URL が正しくないときは、理由 (画面に出す文)</returns>
    public static BacklogLocationResult Parse(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return BacklogLocationResult.Failure("URL が正しくありません。https:// から始まる Backlog のファイル画面の URL を入力してください。");
        }

        if (!BacklogDomains.Any(domain => uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)))
        {
            return BacklogLocationResult.Failure("Backlog の URL ではありません (backlog.jp / backlog.com / backlogtool.com)。");
        }

        // AbsolutePath はエンコードされたまま。区切りの / で分けてから、各セグメントをデコードする
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !segments[0].Equals("file", StringComparison.Ordinal))
        {
            return BacklogLocationResult.Failure("Backlog のファイル画面の URL ではありません。");
        }

        var projectKey = Uri.UnescapeDataString(segments[1]);
        var folderPath = string.Join('/', segments.Skip(2).Select(Uri.UnescapeDataString));
        return BacklogLocationResult.Success(new BacklogLocation(uri.Host, projectKey, folderPath));
    }
}
