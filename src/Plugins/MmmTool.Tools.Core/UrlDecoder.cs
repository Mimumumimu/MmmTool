namespace MmmTool.Tools.Core;

/// <summary>URL の文字コード (%E6%97%A5 のような並び)を、元の文字に戻す</summary>
public static class UrlDecoder
{
    /// <summary>文字列の中の %XX の並びを、UTF-8 として元の文字に戻す</summary>
    /// <param name="text">戻す文字列 (URL・複数行でもよい)</param>
    /// <returns>戻した文字列。UTF-8 として読めない %XX の並びは、そのまま残る</returns>
    /// <remarks>
    /// `%2F` や `%20` のような記号も戻す。結果は読むための文字列で、リンクとして開く用途ではない。
    /// `+` は空白に直さない (パスの `+` は、そのままの文字のため)。
    /// </remarks>
    public static string Decode(string text) => Uri.UnescapeDataString(text);
}
