namespace MmmTool.Core.Links;

/// <summary>
/// リンクメニューの 1 要素。
/// </summary>
/// <remarks>
/// 子要素のリストを持てばフォルダ（空でもよい）、名前が <see cref="SeparatorName"/> なら区切り線、それ以外はリンク。
/// </remarks>
public sealed class LinkNode
{
    /// <summary>区切り線を表す名前。</summary>
    public const string SeparatorName = "-";

    /// <summary>表示名</summary>
    public string Name { get; set; } = "";

    /// <summary>開くパス（リンクのとき）。</summary>
    public string? Path { get; set; }

    /// <summary>子要素（フォルダのとき）。</summary>
    public List<LinkNode>? Children { get; set; }
}
