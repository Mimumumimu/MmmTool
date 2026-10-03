using MmmSdk.Core.Paths;
using MmmSdk.WinUI.Tray;
using MmmTool.Core.Links;

namespace MmmTool.Features.Links;

/// <summary>
/// トレイメニューの「リンク」。リンク編集ページで保存した構成を階層メニューにする。
/// </summary>
/// <param name="links">リンクメニューの構成</param>
/// <param name="opener">パスを開く処理</param>
public sealed class LinkTrayMenuSource(LinkMenuService links, IPathOpener opener) : ITrayMenuSource
{
    /// <inheritdoc />
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
    /// <param name="node">リンクメニューの要素</param>
    /// <returns>トレイメニューの項目</returns>
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
