namespace MmmTool.Notes.Core;

/// <summary>
/// ノートの表示ページが使う仮想ホスト名と、カードの操作の内部リンク。本文のリンク (http / https / file)の判定。
/// </summary>
/// <remarks>
/// カードの「編集」「削除」「復元」は、表示ページが持つ仮想ホストの URL (<see cref="ActionUrl"/>)で表し、ページの移動を横取りして、アプリが処理する。
/// 本文に書いた <c>&lt;a&gt;</c> が、この操作になりすまして、ほかの人のノートの削除などを呼ばないよう、本文のリンクは仮想ホストを指せない (<see cref="TryNormalizeHref"/>)。
/// </remarks>
public static class NoteLinks
{
    /// <summary>表示ページの仮想ホスト名 (実在しない名前。<c>.invalid</c> は、実在しないことが決まっている)</summary>
    public const string PageHost = "notes.mmmtool.invalid";

    /// <summary>表示ページの URL</summary>
    public const string PageUrl = "https://" + PageHost + "/index.html";

    /// <summary>本文のリンクの最大の長さ</summary>
    private const int MaxHrefLength = 2000;

    /// <summary>カードの操作の内部リンクの URL を作る</summary>
    /// <param name="action">操作</param>
    /// <param name="noteId">ノートの番号</param>
    /// <returns>内部リンクの URL</returns>
    public static string ActionUrl(NoteAction action, int noteId)
        => $"https://{PageHost}/action/{ActionName(action)}/{noteId}";

    /// <summary>内部リンクの URL から、操作とノートの番号を取り出す</summary>
    /// <param name="uri">移動先の URL</param>
    /// <param name="action">操作</param>
    /// <param name="noteId">ノートの番号</param>
    /// <returns>カードの操作の内部リンクなら true</returns>
    public static bool TryParseAction(string uri, out NoteAction action, out int noteId)
    {
        action = default;
        noteId = 0;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !IsPageHost(parsed))
        {
            return false;
        }

        // /action/<操作>/<番号>
        var segments = parsed.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not ["action", var name, var number] || !int.TryParse(number, out noteId) || noteId <= 0)
        {
            return false;
        }
        foreach (var candidate in Enum.GetValues<NoteAction>())
        {
            if (name == ActionName(candidate))
            {
                action = candidate;
                return true;
            }
        }
        return false;
    }

    /// <summary>本文のリンク先を、ページに出す形にする (<c>file:</c> は、仮想ホストの内部リンクに包む)</summary>
    /// <param name="href">正規化したリンク先 (<see cref="TryNormalizeHref"/>の結果)</param>
    /// <returns>ページの <c>href</c> に出す URL</returns>
    /// <remarks>
    /// 仮想ホストの https のページから <c>file:</c> のリンクを押しても、WebView2 (Chromium)は、ローカルのファイルへの移動を、移動が始まる前に止める
    /// (アプリが横取りする <c>NavigationStarting</c> が来ない)。そのため、<c>file:</c> は、仮想ホストの内部リンクに包んで出し、アプリが横取りして、元の URL を取り出す。
    /// </remarks>
    public static string ToPageHref(string href)
        => href.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? $"https://{PageHost}/open?u={Uri.EscapeDataString(href)}" : href;

    /// <summary>内部リンクの URL から、包まれている本文のリンク先 (<c>file:</c>)を取り出す</summary>
    /// <param name="uri">移動先の URL</param>
    /// <param name="target">取り出したリンク先</param>
    /// <returns><see cref="ToPageHref"/> が包んだ内部リンクで、中身が開いてよい URL なら true</returns>
    public static bool TryParseOpen(string uri, out Uri target)
    {
        target = null!;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !IsPageHost(parsed) || parsed.AbsolutePath != "/open" || !parsed.Query.StartsWith("?u=", StringComparison.Ordinal))
        {
            return false;
        }

        var inner = Uri.UnescapeDataString(parsed.Query[3..]);
        if (!Uri.TryCreate(inner, UriKind.Absolute, out var innerUri) || !IsOpenable(innerUri))
        {
            return false;
        }
        target = innerUri;
        return true;
    }

    /// <summary>表示ページの仮想ホストの URL か</summary>
    /// <param name="uri">移動先の URL</param>
    /// <returns>仮想ホストの https の URL なら true</returns>
    public static bool IsPageUri(string uri)
        => Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && IsPageHost(parsed);

    /// <summary>ページの中のリンクとして、開いてよい URL か (http・https・file)</summary>
    /// <param name="uri">開く URL</param>
    /// <returns>開いてよいなら true</returns>
    public static bool IsOpenable(Uri uri)
        => uri.Scheme is "http" or "https" or "file" && !IsPageHost(uri);

    /// <summary>本文のリンクとして使える URL かを調べ、書き直した形にする</summary>
    /// <param name="raw">本文に書かれたリンク先</param>
    /// <param name="href">使えるときの、正規化した URL (<c>http</c>・<c>https</c>・<c>file</c>)</param>
    /// <returns>使えるなら true。それ以外の種類 (独自のスキーム・<c>javascript:</c> など)・空白や引用符を含むもの・仮想ホストは false</returns>
    public static bool TryNormalizeHref(string raw, out string href)
    {
        href = "";
        var text = raw.Trim();
        if (text.Length is 0 or > MaxHrefLength)
        {
            return false;
        }
        foreach (var c in text)
        {
            if (c <= ' ' || c is '"' or '<' or '>' or '\\' or '\'' or '`')
            {
                return false;
            }
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !IsOpenable(uri))
        {
            return false;
        }
        if (uri.Scheme != "file" && uri.Host.Length == 0)
        {
            return false;
        }
        href = uri.AbsoluteUri;
        return true;
    }

    /// <summary>ページの HTML に出したリンク先 (本文のリンク)を、すべて集める</summary>
    /// <param name="html">アプリが組み立てたページの HTML</param>
    /// <returns>リンク先の正規化した URL (<see cref="Uri.AbsoluteUri"/>)の集まり。カードの操作の内部リンクは含まない</returns>
    /// <remarks>
    /// 押したリンクを開いてよいかの判定に使う (開くのは、今のページに出したリンクだけ)。ファイルをページに落として、そのファイルへ移ろうとしたときなど、
    /// ページに出していないものは、開かない。HTML はアプリが組み立てたもの (<c>&lt;a href="…"&gt;</c> の形だけ)なので、その形だけを集める。
    /// </remarks>
    public static HashSet<string> CollectHrefs(string html)
    {
        var hrefs = new HashSet<string>(StringComparer.Ordinal);
        const string Marker = "<a href=\"";
        var position = 0;
        while ((position = html.IndexOf(Marker, position, StringComparison.Ordinal)) >= 0)
        {
            var start = position + Marker.Length;
            var end = html.IndexOf('"', start);
            if (end < 0)
            {
                break;
            }
            position = end;

            // 属性の値は、& < > " を HTML の文字に直してある。元に戻して、URL にする
            var value = html[start..end].Replace("&quot;", "\"", StringComparison.Ordinal).Replace("&lt;", "<", StringComparison.Ordinal).Replace("&gt;", ">", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);
            if (TryParseOpen(value, out var wrapped))
            {
                hrefs.Add(wrapped.AbsoluteUri);
            }
            else if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsOpenable(uri))
            {
                hrefs.Add(uri.AbsoluteUri);
            }
        }
        return hrefs;
    }

    /// <summary>ファイル・フォルダーのパスを、リンク先の URL (<c>file:</c>)にする</summary>
    /// <param name="path">ファイル・フォルダーのパス (<c>C:\…</c> または <c>\\server\…</c>)</param>
    /// <returns>URL。パスとして読めないときは、そのまま返す</returns>
    public static string FileHref(string path)
        => Path.IsPathRooted(path) && Uri.TryCreate(path, UriKind.Absolute, out var uri) ? uri.AbsoluteUri : path;

    /// <summary>仮想ホストの URL か</summary>
    /// <param name="uri">URL</param>
    /// <returns>https で、仮想ホストなら true</returns>
    private static bool IsPageHost(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.Host, PageHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>操作の、URL の中の名前</summary>
    /// <param name="action">操作</param>
    /// <returns>小文字の名前</returns>
    private static string ActionName(NoteAction action) => action.ToString().ToLowerInvariant();
}
