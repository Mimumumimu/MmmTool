namespace MmmTool.Links.Core;

/// <summary>リンクメニューの 1 要素</summary>
/// <remarks>
/// 種類は <see cref="Kind"/> で明示する。
/// 保存するときは必ず書く。
/// 名前から決めると、「-」という名前のリンクが区切り線に変わってしまうため。
/// <see cref="Kind"/> が無い古い JSON・知らない値の JSON は <see cref="ResolveKind"/> が以前の決め方 (子要素があればフォルダ、名前が <see cref="LegacySeparatorName"/> なら区切り線、それ以外はリンク)で読み替える。
/// </remarks>
public sealed class LinkNode
{
    /// <summary>種類が書かれていない古い JSON で、区切り線を表していた名前</summary>
    public const string LegacySeparatorName = "-";

    /// <summary>種類で、"link" / "folder" / "separator" のどれか</summary>
    /// <remarks>
    /// 書かれていないときは null
    /// 文字列で持つ理由は <see cref="LinkNodeKindNames"/>
    /// </remarks>
    public string? Kind { get; set; }

    /// <summary>表示名</summary>
    public string Name { get; set; } = "";

    /// <summary>開くパスでリンクのときに使う</summary>
    public string? Path { get; set; }

    /// <summary>子要素でフォルダのときに使う</summary>
    public List<LinkNode>? Children { get; set; }

    /// <summary>種類を決める</summary>
    /// <returns>
    /// <see cref="Kind"/> が使える値ならそれ。
    /// 無い・知らない値のとき (古い JSON・書き間違い)は子要素があればフォルダ、名前が <see cref="LegacySeparatorName"/> なら区切り線、それ以外はリンク。
    /// </returns>
    public LinkNodeKind ResolveKind()
    {
        if (LinkNodeKindNames.Parse(Kind) is { } kind)
        {
            return kind;
        }

        if (Children is not null)
        {
            return LinkNodeKind.Folder;
        }

        return Name == LegacySeparatorName ? LinkNodeKind.Separator : LinkNodeKind.Link;
    }
}
