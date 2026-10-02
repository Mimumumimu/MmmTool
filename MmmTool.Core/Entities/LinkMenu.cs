namespace MmmTool.Core.Entities;

/// <summary>
/// リンクメニューの構成（Data/Links.json）。
/// DB には載せないローカル専用の設定で、人が手で編集しやすいよう ID を持たない入れ子の形で保存する。
/// </summary>
public sealed class LinkMenu
{
    public List<LinkNode>? Items { get; set; } = [];
}

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

    public string Name { get; set; } = "";

    /// <summary>開くパス（リンクのとき）。</summary>
    public string? Path { get; set; }

    /// <summary>子要素（フォルダのとき）。</summary>
    public List<LinkNode>? Children { get; set; }
}
