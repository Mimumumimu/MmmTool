using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Core.Entities;

namespace MmmTool.ViewModels;

public enum LinkItemKind
{
    Link,

    /// <summary>子を持てる中間ノード（空でもよい）。</summary>
    Folder,

    Separator,
}

/// <summary>
/// リンク編集ツリーの 1 要素（編集用）。保存時に <see cref="LinkNode"/> へ変換する。
/// </summary>
/// <remarks>
/// 種類は作成時（読み込み・追加時）に決めて変えない。名前で決めると、入力途中に「-」と打っただけで区切り線に変わってしまうため。
/// </remarks>
public sealed partial class LinkTreeItem : ObservableObject
{
    private LinkTreeItem(LinkItemKind kind, string name, string path, ObservableCollection<LinkTreeItem>? children)
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

    public LinkItemKind Kind { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>開くパス（リンクのとき）。</summary>
    [ObservableProperty]
    public partial string Path { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    public partial bool IsExpanded { get; set; }

    /// <summary>子要素。フォルダのときだけ持ち、それ以外は null。</summary>
    public ObservableCollection<LinkTreeItem>? Children { get; }

    public bool IsFolder => Kind == LinkItemKind.Folder;

    public bool IsSeparator => Kind == LinkItemKind.Separator;

    public bool IsNotSeparator => !IsSeparator;

    /// <summary>種類のアイコン（フォルダは開閉で変える）。</summary>
    public string Glyph => Kind switch
    {
        LinkItemKind.Folder => IsExpanded ? "" : "",
        _ => "",
    };

    public static LinkTreeItem CreateLink() => new(LinkItemKind.Link, "新しいリンク", "", null);

    public static LinkTreeItem CreateFolder() => new(LinkItemKind.Folder, "新しいフォルダー", "", []);

    public static LinkTreeItem CreateSeparator() => new(LinkItemKind.Separator, LinkNode.SeparatorName, "", null);

    public static LinkTreeItem From(LinkNode node)
    {
        // 子要素のリストがあればフォルダ（パスより優先）
        if (node.Children is not null)
        {
            return new LinkTreeItem(LinkItemKind.Folder, node.Name, "", new ObservableCollection<LinkTreeItem>(node.Children.Select(From)));
        }

        return node.Name == LinkNode.SeparatorName
            ? CreateSeparator()
            : new LinkTreeItem(LinkItemKind.Link, node.Name, node.Path ?? "", null);
    }

    public LinkNode ToNode() => Kind switch
    {
        LinkItemKind.Folder => new LinkNode { Name = Name, Children = [.. Children!.Select(child => child.ToNode())] },
        LinkItemKind.Separator => new LinkNode { Name = LinkNode.SeparatorName },
        _ => new LinkNode { Name = Name, Path = string.IsNullOrWhiteSpace(Path) ? null : Path.Trim() },
    };

    /// <summary>子孫の数（フォルダのとき）。</summary>
    public int CountDescendants() => Children?.Sum(child => 1 + child.CountDescendants()) ?? 0;
}
