using System.Text;
using System.Text.RegularExpressions;
using MmmSdk.Core.Components.TextLinks;

namespace MmmTool.Notes.Core;

/// <summary>
/// ノートの本文を、許可したタグだけを、アプリが決まった形で組み立て直して、HTML にする。
/// </summary>
/// <remarks>
/// <para>
/// 取り除く方式ではなく、組み立て直す方式。本文の文字は、すべて HTML の文字 (<c>&amp;</c> <c>&lt;</c> など)に直して出し、
/// 許可したタグ (太字・斜体・下線・取消線・改行・箇条書き・パレットの色・リンク)に当たる部分だけを、アプリが決まった形の
/// タグ (属性も、アプリが決めた形だけ)で出す。そのため、出力には、アプリが作ったタグしか入らない。
/// 外部への接続の型を含むパッケージ (HTML の除去用の部品が使う AngleSharp など)は、MmmTool に入れない決まりのため、この方式にしている。
/// </para>
/// <para>
/// 許可していないタグは、タグだけを取り除く (中の文字は残す)。<c>script</c> と <c>style</c> は、中身ごと取り除く。
/// 閉じていないタグは最後に閉じ、対応しない閉じタグは捨てる。
/// 本文の <c>http://</c> と <c>https://</c> で始まる文字は、リンクにする (<c>&lt;a&gt;</c> の中を除く)。
/// </para>
/// </remarks>
public sealed partial class NoteBodyFormatter : INoteBodyFormatter
{
    /// <summary>中身ごと取り除くブロック (<c>script</c>・<c>style</c>)</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DroppedBlockRegex();

    /// <summary>タグの 1 つ。許可するものは名前つきのグループで見分け、それ以外のタグ (最後の候補)は取り除く</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(
        @"(?<comment><!--.*?-->)"
        + @"|<(?<close>/?)(?<simple>strong|b|em|i|u|s|ul|ol|li)\s*>"
        + @"|(?<br><br\s*/?>)"
        + @"|<span\s+style\s*=\s*(?:""color\s*:\s*(?<c1>#[0-9A-Fa-f]{6})\s*;?\s*""|'color\s*:\s*(?<c2>#[0-9A-Fa-f]{6})\s*;?\s*')\s*>"
        + @"|(?<spanplain><span\s*>)"
        + @"|(?<spanend></span\s*>)"
        + @"|<a\s+href\s*=\s*(?:""(?<h1>[^""<>]*)""|'(?<h2>[^'<>]*)')\s*>"
        + @"|(?<aend></a\s*>)"
        + @"|(?<other></?[A-Za-z][^<>]*>)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    /// <summary>箇条書きのタグのすぐ前の改行 (タグの前後の改行で、空きが出ないように)</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"<br>(?=</?(?:ul|ol|li)>)")]
    private static partial Regex BreakBeforeListRegex();

    /// <summary>箇条書きのタグのすぐ後の改行</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"(?<=</?(?:ul|ol|li)>)<br>")]
    private static partial Regex BreakAfterListRegex();

    /// <summary>出力に出した、まだ閉じていないタグ</summary>
    /// <param name="Name">タグの名前</param>
    /// <param name="Emitted">開きタグを出力に出したか (出していないものは、閉じタグも出さない)</param>
    private readonly record struct OpenTag(string Name, bool Emitted);

    /// <summary><c>file:</c> のリンクを、リンクにするか</summary>
    private readonly bool _allowFileLinks;

    /// <summary>
    /// <c>file:</c> のリンクも、リンクにする整形を作る。パソコンのアプリが使う。
    /// </summary>
    public NoteBodyFormatter()
        : this(allowFileLinks: true)
    {
    }

    /// <summary>
    /// 整形を作る。
    /// </summary>
    /// <param name="allowFileLinks"><c>file:</c> のリンクを、リンクにするか。false のときは、中の文字だけを残す (ファイルを開けない iPhone の Web 版)</param>
    public NoteBodyFormatter(bool allowFileLinks)
    {
        _allowFileLinks = allowFileLinks;
    }

    /// <inheritdoc />
    public string ToHtml(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return "";
        }

        var text = body.Replace("\r\n", "\n").Replace('\r', '\n');
        text = DroppedBlockRegex().Replace(text, "");

        var output = new StringBuilder(text.Length + 64);
        var open = new List<OpenTag>();
        var position = 0;
        foreach (Match tag in TagRegex().Matches(text))
        {
            AppendText(text[position..tag.Index], output, InsideAnchor(open));
            position = tag.Index + tag.Length;
            HandleTag(tag, output, open);
        }
        AppendText(text[position..], output, InsideAnchor(open));

        for (var i = open.Count - 1; i >= 0; i--)
        {
            CloseTag(open[i], output);
        }
        return BreakAfterListRegex().Replace(BreakBeforeListRegex().Replace(output.ToString(), ""), "");
    }

    /// <summary>リンクの中か (開きタグを出力に出した <c>a</c> が、まだ閉じていないか)</summary>
    /// <param name="open">閉じていないタグ</param>
    /// <returns>リンクの中なら true</returns>
    private static bool InsideAnchor(List<OpenTag> open) => open.Any(tag => tag is { Name: "a", Emitted: true });

    /// <summary>タグ 1 つを処理する (許可したタグは、決まった形で出力に出し、それ以外は捨てる)</summary>
    /// <param name="tag">見つけたタグ</param>
    /// <param name="output">出力</param>
    /// <param name="open">閉じていないタグ</param>
    private void HandleTag(Match tag, StringBuilder output, List<OpenTag> open)
    {
        if (tag.Groups["simple"].Success)
        {
            var name = tag.Groups["simple"].Value.ToLowerInvariant();
            if (tag.Groups["close"].Value.Length > 0)
            {
                CloseIfLast(name, output, open);
            }
            else
            {
                output.Append('<').Append(name).Append('>');
                open.Add(new OpenTag(name, true));
            }
        }
        else if (tag.Groups["br"].Success)
        {
            output.Append("<br>");
        }
        else if (tag.Groups["c1"].Success || tag.Groups["c2"].Success)
        {
            var hex = tag.Groups["c1"].Success ? tag.Groups["c1"].Value : tag.Groups["c2"].Value;
            output.Append(NoteColorPalette.TryFind(hex) is { } color ? $"<span class=\"nc{color.Index}\">" : "<span>");
            open.Add(new OpenTag("span", true));
        }
        else if (tag.Groups["spanplain"].Success)
        {
            output.Append("<span>");
            open.Add(new OpenTag("span", true));
        }
        else if (tag.Groups["spanend"].Success)
        {
            CloseIfLast("span", output, open);
        }
        else if (tag.Groups["h1"].Success || tag.Groups["h2"].Success)
        {
            var raw = tag.Groups["h1"].Success ? tag.Groups["h1"].Value : tag.Groups["h2"].Value;
            if (NoteLinks.TryNormalizeHref(raw, out var href) && (_allowFileLinks || !href.StartsWith("file:", StringComparison.OrdinalIgnoreCase)))
            {
                output.Append("<a href=\"").Append(EscapeAttribute(NoteLinks.ToPageHref(href))).Append("\">");
                open.Add(new OpenTag("a", true));
            }
            else
            {
                // 使えないリンク先は、リンクにしない (中の文字は残す)。閉じタグの対応だけ数える
                open.Add(new OpenTag("a", false));
            }
        }
        else if (tag.Groups["aend"].Success)
        {
            CloseIfLast("a", output, open);
        }

        // 注釈 (comment)と、許可していないタグ (other)は、何も出さずに捨てる
    }

    /// <summary>いちばん新しい開きタグが、指定の名前のときだけ閉じる (対応しない閉じタグは捨てる)</summary>
    /// <param name="name">閉じるタグの名前</param>
    /// <param name="output">出力</param>
    /// <param name="open">閉じていないタグ</param>
    private static void CloseIfLast(string name, StringBuilder output, List<OpenTag> open)
    {
        if (open.Count > 0 && open[^1].Name == name)
        {
            CloseTag(open[^1], output);
            open.RemoveAt(open.Count - 1);
        }
    }

    /// <summary>閉じタグを出力に出す (開きタグを出していないものは、何も出さない)</summary>
    /// <param name="tag">閉じるタグ</param>
    /// <param name="output">出力</param>
    private static void CloseTag(OpenTag tag, StringBuilder output)
    {
        if (tag.Emitted)
        {
            output.Append("</").Append(tag.Name).Append('>');
        }
    }

    /// <summary>タグではない文字の部分を、出力に出す (URL はリンクにし、それ以外は HTML の文字に直す)</summary>
    /// <param name="segment">文字の部分</param>
    /// <param name="output">出力</param>
    /// <param name="insideAnchor">リンクの中か (リンクの中の URL は、二重にリンクにしない)</param>
    private static void AppendText(string segment, StringBuilder output, bool insideAnchor)
    {
        if (segment.Length == 0)
        {
            return;
        }
        if (insideAnchor)
        {
            AppendEscaped(segment, output);
            return;
        }

        var position = 0;
        foreach (var link in TextLinkFinder.Find(segment))
        {
            // 表示ページの仮想ホストを指す URL は、カードの操作になりすませるので、リンクにしない
            if (NoteLinks.IsPageUri(link.Href))
            {
                continue;
            }

            AppendEscaped(segment[position..link.Start], output);
            output.Append("<a href=\"").Append(EscapeAttribute(link.Href)).Append("\">");
            AppendEscaped(segment.Substring(link.Start, link.Length), output);
            output.Append("</a>");
            position = link.Start + link.Length;
        }
        AppendEscaped(segment[position..], output);
    }

    /// <summary>文字を、HTML の文字に直して出力に出す (改行は <c>&lt;br&gt;</c> にする)</summary>
    /// <param name="text">文字</param>
    /// <param name="output">出力</param>
    private static void AppendEscaped(string text, StringBuilder output)
    {
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': output.Append("&amp;"); break;
                case '<': output.Append("&lt;"); break;
                case '>': output.Append("&gt;"); break;
                case '"': output.Append("&quot;"); break;
                case '\'': output.Append("&#39;"); break;
                case '\n': output.Append("<br>"); break;
                default: output.Append(c); break;
            }
        }
    }

    /// <summary>属性の値として、HTML の文字に直す</summary>
    /// <param name="value">値</param>
    /// <returns>直した文字列</returns>
    private static string EscapeAttribute(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}
