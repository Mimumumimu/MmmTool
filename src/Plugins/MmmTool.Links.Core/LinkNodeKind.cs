namespace MmmTool.Links.Core;

/// <summary>リンクメニューの要素の種類</summary>
/// <remarks>JSON では <c>"link"</c> / <c>"folder"</c> / <c>"separator"</c> の文字列で書く (<see cref="LinkNodeKindNames"/>)。</remarks>
public enum LinkNodeKind
{
    /// <summary>リンク (開く先を持つ)</summary>
    Link,

    /// <summary>子を持てる中間ノード (空でもよい)。</summary>
    Folder,

    /// <summary>区切り線</summary>
    Separator,
}
