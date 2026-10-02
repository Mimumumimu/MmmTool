using MmmTool.Core.Entities;
using MmmTool.Core.Services;

namespace MmmTool.Services.Tray;

/// <summary>
/// トレイメニューの「リンク」。リンク編集ページで保存した構成を階層メニューにする。
/// </summary>
public sealed class LinkTrayMenuSource(LinkMenuService links, LinkOpener opener) : ITrayMenuSource
{
    public IReadOnlyList<TrayMenuItem> GetItems()
    {
        IReadOnlyList<TrayMenuItem> children = links.LoadError is not null
            ? [TrayMenuItem.Disabled("Links.json を読み込めません（リンク画面で確認してください）")]
            : links.Current?.Items is { Count: > 0 } items
                ? [.. items.Select(ToMenuItem)]
                : [TrayMenuItem.Disabled("リンクはまだありません")];

        return [TrayMenuItem.Submenu("リンク", children)];
    }

    /// <summary>
    /// 子要素があればサブメニュー、名前が「-」なら区切り線、パスがあれば開く項目、どれでもなければ押せない項目にする。
    /// </summary>
    /// <remarks>中身が空のフォルダも、開く先が無いので押せない項目として出す。</remarks>
    private TrayMenuItem ToMenuItem(LinkNode node)
    {
        if (node.Children is { Count: > 0 } children)
        {
            return TrayMenuItem.Submenu(node.Name, [.. children.Select(ToMenuItem)]);
        }
        if (node.Name == LinkNode.SeparatorName)
        {
            return TrayMenuItem.Separator;
        }
        if (!string.IsNullOrWhiteSpace(node.Path))
        {
            var path = node.Path;
            return TrayMenuItem.Command(node.Name, () => opener.OpenAsync(path));
        }
        return TrayMenuItem.Disabled(node.Name);
    }
}
