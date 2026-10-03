namespace MmmTool.Features.Links;

/// <summary>リンクツリーの要素の種類</summary>
public enum LinkItemKind
{
    /// <summary>リンク（開く先を持つ）</summary>
    Link,

    /// <summary>子を持てる中間ノード（空でもよい）。</summary>
    Folder,

    /// <summary>区切り線</summary>
    Separator,
}
