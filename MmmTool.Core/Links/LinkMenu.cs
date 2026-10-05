namespace MmmTool.Core.Links;

/// <summary>リンクメニューの構成 (Data/Links.json)</summary>
/// <remarks>DB には載せないローカル専用の設定で、人が手で編集しやすいよう ID を持たない入れ子の形で保存する。</remarks>
public sealed class LinkMenu
{
    /// <summary>リンクの一覧 (最上位)</summary>
    /// <remarks>JSON に <c>null</c> と書かれていたときは、読み込み (Repository)で空にする。</remarks>
    public List<LinkNode> Items { get; set; } = [];

    /// <summary>設定の誤り (知らない <c>kind</c> の値)を探す</summary>
    /// <returns>ユーザーに見せる警告のメッセージ。誤りが無ければ空</returns>
    /// <remarks>
    /// 手で編集した JSON なので、知らない値は読み込みでは無視する (名前・子要素から種類を決める)。ただし、黙って無視すると、書き間違いに気づけないので、
    /// 読み込んだあとにこれで調べて、画面で知らせる。
    /// </remarks>
    public IReadOnlyList<string> Validate()
    {
        List<string> problems = [];
        CheckNodes(Items, "リンク", problems);
        return problems;
    }

    /// <summary>ノードの木を調べて、誤りを足す</summary>
    /// <param name="nodes">調べるノード</param>
    /// <param name="path">ここまでの表示名のつながり (メッセージに出す)</param>
    /// <param name="problems">誤りのメッセージの足し先</param>
    private static void CheckNodes(List<LinkNode>? nodes, string path, List<string> problems)
    {
        foreach (var node in nodes ?? [])
        {
            var here = $"{path} › {node.Name}";
            if (!string.IsNullOrWhiteSpace(node.Kind) && LinkNodeKindNames.Parse(node.Kind) is null)
            {
                problems.Add($"「{here}」の kind「{node.Kind}」は使えません ({string.Join(" / ", LinkNodeKindNames.Allowed)} のどれかを書きます)。この設定は無視して、名前・子要素から種類を決めます。");
            }

            CheckNodes(node.Children, here, problems);
        }
    }
}
