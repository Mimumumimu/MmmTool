namespace MmmTool.Core.Links;

/// <summary>リンクメニューの構成（Data/Links.json）</summary>
/// <remarks>DB には載せないローカル専用の設定で、人が手で編集しやすいよう ID を持たない入れ子の形で保存する。</remarks>
public sealed class LinkMenu
{
    /// <summary>リンクの一覧（最上位）</summary>
    public List<LinkNode>? Items { get; set; } = [];
}
