namespace MmmTool.Core.Links;

/// <summary><see cref="LinkNodeKind"/> と、JSON に書く文字列の変換</summary>
/// <remarks>
/// 手で編集した JSON の書き間違い (<c>"folders"</c> など)で、ファイル全体が読めなくならないよう、Entity は種類を文字列で持つ (CLI補助の <c>switchTo</c> / <c>focus</c> と同じ)。
/// 知らない値は読み込みでは無視し (<see cref="LinkNode.ResolveKind"/>)、読み込んだあとに <see cref="LinkMenu.Validate"/> で調べて、画面で知らせる。
/// </remarks>
public static class LinkNodeKindNames
{
    /// <summary>JSON に書く、リンクの文字列</summary>
    private const string Link = "link";

    /// <summary>JSON に書く、フォルダの文字列</summary>
    private const string Folder = "folder";

    /// <summary>JSON に書く、区切り線の文字列</summary>
    private const string Separator = "separator";

    /// <summary>使える値の一覧 (メッセージに出す)</summary>
    public static IReadOnlyList<string> Allowed { get; } = [Link, Folder, Separator];

    /// <summary>種類を、JSON に書く文字列にする</summary>
    /// <param name="kind">種類</param>
    /// <returns>JSON に書く文字列</returns>
    public static string ToJsonValue(LinkNodeKind kind) => kind switch
    {
        LinkNodeKind.Folder => Folder,
        LinkNodeKind.Separator => Separator,
        _ => Link,
    };

    /// <summary>JSON に書かれた文字列を、種類にする</summary>
    /// <param name="value">JSON に書かれた文字列</param>
    /// <returns>種類。省略・知らない値なら null</returns>
    /// <remarks>大文字小文字は区別しない。</remarks>
    public static LinkNodeKind? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Link => LinkNodeKind.Link,
        Folder => LinkNodeKind.Folder,
        Separator => LinkNodeKind.Separator,
        _ => null,
    };
}
