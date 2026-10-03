using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Core.Links;

namespace MmmTool.Features.Links.Main;

/// <summary>
/// リンク編集ツリーの 1 要素（編集用）。保存時に <see cref="LinkNode"/> へ変換する。
/// </summary>
/// <remarks>
/// 種類は作成時（読み込み・追加時）に決めて変えない。名前で決めると、入力途中に「-」と打っただけで区切り線に変わってしまうため。
/// 保存する JSON にも種類を書くので、「-」という名前のリンクも、読み直して区切り線に変わらない。
/// </remarks>
public sealed partial class LinkTreeItem : ObservableObject
{
    /// <summary>要素を作る</summary>
    /// <param name="kind">種類</param>
    /// <param name="name">表示名</param>
    /// <param name="path">開くパス</param>
    /// <param name="children">子要素（フォルダのとき。それ以外は null）</param>
    /// <remarks>生成は <see cref="CreateLink"/> などから行う。</remarks>
    private LinkTreeItem(LinkNodeKind kind, string name, string path, ObservableCollection<LinkTreeItem>? children)
    {
        Kind = kind;
        Name = name;
        Path = path;
        Children = children;

        if (children is not null)
        {
            // フォルダは開いた状態が基本（閉じるのはユーザーが閉じたときだけ）。中に項目が入ったら（追加・ドロップ）開く
            IsExpanded = true;
            children.CollectionChanged += (_, e) =>
            {
                if (e.Action is NotifyCollectionChangedAction.Add)
                {
                    IsExpanded = true;
                }
            };
        }
    }

    /// <summary>種類</summary>
    /// <remarks>作成時に決めて変えない。</remarks>
    public LinkNodeKind Kind { get; }

    /// <summary>表示名</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>開くパス（リンクのとき）。</summary>
    [ObservableProperty]
    public partial string Path { get; set; }

    /// <summary>フォルダが開いているか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    public partial bool IsExpanded { get; set; }

    /// <summary>子要素</summary>
    /// <remarks>フォルダのときだけ持ち、それ以外は null。</remarks>
    public ObservableCollection<LinkTreeItem>? Children { get; }

    /// <summary>フォルダか</summary>
    public bool IsFolder => Kind == LinkNodeKind.Folder;

    /// <summary>区切り線か</summary>
    public bool IsSeparator => Kind == LinkNodeKind.Separator;

    /// <summary>区切り線ではないか</summary>
    public bool IsNotSeparator => !IsSeparator;

    /// <summary>種類のアイコン</summary>
    /// <remarks>フォルダは開閉で変える。</remarks>
    public string Glyph => Kind switch
    {
        LinkNodeKind.Folder => IsExpanded ? "" : "",
        _ => "",
    };

    /// <summary>新しいリンクを作る</summary>
    /// <returns>新しいリンクの要素</returns>
    public static LinkTreeItem CreateLink() => new(LinkNodeKind.Link, "新しいリンク", "", null);

    /// <summary>新しいフォルダーを作る</summary>
    /// <returns>新しいフォルダーの要素</returns>
    public static LinkTreeItem CreateFolder() => new(LinkNodeKind.Folder, "新しいフォルダー", "", []);

    /// <summary>新しい区切り線を作る</summary>
    /// <returns>新しい区切り線の要素</returns>
    public static LinkTreeItem CreateSeparator() => new(LinkNodeKind.Separator, "", "", null);

    /// <summary>保存されている構成から編集用の要素を作る</summary>
    /// <param name="node">保存されている構成の 1 要素</param>
    /// <returns>編集用の要素</returns>
    public static LinkTreeItem From(LinkNode node) => node.ResolveKind() switch
    {
        LinkNodeKind.Folder => new LinkTreeItem(
            LinkNodeKind.Folder, node.Name, "", new ObservableCollection<LinkTreeItem>((node.Children ?? []).Select(From))),
        LinkNodeKind.Separator => CreateSeparator(),
        _ => new LinkTreeItem(LinkNodeKind.Link, node.Name, node.Path ?? "", null),
    };

    /// <summary>保存用の構成に変換する</summary>
    /// <returns>保存用の構成の要素（種類を必ず書く）</returns>
    public LinkNode ToNode() => Kind switch
    {
        LinkNodeKind.Folder => new LinkNode { Kind = LinkNodeKindNames.ToJsonValue(LinkNodeKind.Folder), Name = Name, Children = [.. Children!.Select(child => child.ToNode())] },
        LinkNodeKind.Separator => new LinkNode { Kind = LinkNodeKindNames.ToJsonValue(LinkNodeKind.Separator) },
        _ => new LinkNode { Kind = LinkNodeKindNames.ToJsonValue(LinkNodeKind.Link), Name = Name, Path = string.IsNullOrWhiteSpace(Path) ? null : Path.Trim() },
    };

    /// <summary>子孫の数（フォルダのとき）。</summary>
    /// <returns>子孫の数</returns>
    public int CountDescendants() => Children?.Sum(child => 1 + child.CountDescendants()) ?? 0;
}
