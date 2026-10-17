using System.Text.RegularExpressions;

namespace MmmTool.Notes.Core;

/// <summary>
/// 本文に、書式のタグを入れる文字列を作る (入力欄の右クリックの助けとファイルのドロップが使う)。
/// </summary>
public static partial class NoteMarkup
{
    /// <summary>色のタグ (開きタグと、閉じタグ)。色を外すときに取り除く</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"<span\s+style\s*=\s*(?:""color\s*:\s*#[0-9A-Fa-f]{6}\s*;?\s*""|'color\s*:\s*#[0-9A-Fa-f]{6}\s*;?\s*')\s*>|</span\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ColorTagRegex();

    /// <summary>文字を、書式のタグで囲む</summary>
    /// <param name="text">囲む文字</param>
    /// <param name="tag">タグの名前 (<c>b</c> <c>i</c> <c>u</c> <c>s</c>)</param>
    /// <returns>タグで囲んだ文字列</returns>
    public static string Wrap(string text, string tag) => $"<{tag}>{text}</{tag}>";

    /// <summary>文字を、色のタグで囲む</summary>
    /// <param name="text">囲む文字</param>
    /// <param name="color">パレットの色</param>
    /// <returns>色のタグで囲んだ文字列 (選んだ範囲の中の、前の色のタグは取り除く)</returns>
    public static string WrapColor(string text, NoteColor color)
        => $"<span style=\"color:{color.Hex}\">{RemoveColor(text)}</span>";

    /// <summary>文字から、色のタグを取り除く (色を「標準」に戻す)</summary>
    /// <param name="text">文字</param>
    /// <returns>色のタグを除いた文字列</returns>
    public static string RemoveColor(string text) => ColorTagRegex().Replace(text, "");

    /// <summary>リンクのタグを作る</summary>
    /// <param name="name">リンク名 (空なら、リンク先をそのまま使う)</param>
    /// <param name="target">リンク先 (URL、またはファイル・フォルダーのパス)</param>
    /// <returns>リンクのタグ</returns>
    public static string Link(string name, string target)
    {
        var href = target.Trim();
        if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !href.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            href = NoteLinks.FileHref(href);
        }

        var label = name.Trim().Replace("<", "", StringComparison.Ordinal).Replace(">", "", StringComparison.Ordinal);
        return $"<a href=\"{href.Replace("\"", "%22", StringComparison.Ordinal)}\">{(label.Length == 0 ? target.Trim() : label)}</a>";
    }

    /// <summary>ドロップしたファイル・フォルダーのパスから、リンクを並べた文字列を作る</summary>
    /// <param name="paths">ドロップしたパス</param>
    /// <returns>リンクを、1 つずつ改行して並べた文字列 (リンク名は、ファイル・フォルダーの名前)</returns>
    public static string LinksFromPaths(IEnumerable<string> paths)
        => string.Join('\n', paths.Select(path => Link(Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : path, path)));
}
