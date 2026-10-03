using MmmSdk.Core.Paths;
using MmmSdk.WinUI.Notifications;
using MmmSdk.WinUI.Tray;
using MmmTool.Core.Links;

namespace MmmTool.Features.Links;

/// <summary>
/// トレイメニューの「リンク」。リンク編集ページで保存した構成を階層メニューにする。
/// </summary>
/// <param name="links">リンクメニューの構成</param>
/// <param name="opener">パスを開く処理</param>
/// <param name="notifications">通知ダイアログの表示（リンクを開けなかったことを知らせる）</param>
public sealed class LinkTrayMenuSource(LinkMenuService links, IPathOpener opener, INotificationDialogService notifications) : ITrayMenuSource
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

    /// <summary>リンクを開く</summary>
    /// <param name="name">リンクの表示名</param>
    /// <param name="path">開くパス</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>パスが存在しない・開けない（<see cref="PathOpenException"/>）ときは、通知ダイアログで知らせる。それ以外の失敗は、バグとして、アプリの安全網が受ける。</remarks>
    private async Task OpenAsync(string name, string path)
    {
        try
        {
            await opener.OpenAsync(path);
        }
        catch (PathOpenException ex)
        {
            notifications.Show($"リンクを開けません: {name}", ex.Message);
        }
    }

    /// <summary>
    /// 区切り線は区切り線に、子要素があればサブメニューに、パスがあれば開く項目に、どれでもなければ押せない項目にする。
    /// </summary>
    /// <param name="node">リンクメニューの要素</param>
    /// <returns>トレイメニューの項目</returns>
    /// <remarks>中身が空のフォルダも、開く先が無いので押せない項目として出す。種類は <see cref="LinkNode.ResolveKind"/> で決める。</remarks>
    private TrayMenuItem ToMenuItem(LinkNode node)
    {
        if (node.ResolveKind() == LinkNodeKind.Separator)
        {
            return TrayMenuItem.Separator;
        }
        if (node.Children is { Count: > 0 } children)
        {
            return TrayMenuItem.Submenu(node.Name, [.. children.Select(ToMenuItem)]);
        }
        if (node.ResolveKind() == LinkNodeKind.Link && !string.IsNullOrWhiteSpace(node.Path))
        {
            var path = node.Path;
            return TrayMenuItem.Command(node.Name, () => OpenAsync(node.Name, path));
        }
        return TrayMenuItem.Disabled(node.Name);
    }
}
