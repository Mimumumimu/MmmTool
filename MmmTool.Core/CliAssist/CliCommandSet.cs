namespace MmmTool.Core.CliAssist;

/// <summary>CLI補助の定型コマンド定義（Data/CliCommands.json）</summary>
/// <remarks>DB には載せないローカル専用の設定で、人が手で編集しやすいよう ID を持たない入れ子の形で保存する。</remarks>
public sealed class CliCommandSet
{
    /// <summary>シェルで打つコマンド（AI エージェント起動前）。</summary>
    /// <remarks>JSON に <c>null</c> と書かれていたときは、読み込み（Repository）で空にする。</remarks>
    public List<CliCommandNode> Shell { get; set; } = [];

    /// <summary>AI エージェント（Claude Code・Kiro 等）のセッション内で打つコマンド（起動後）</summary>
    /// <remarks>ツールごとにフォルダで分ける。JSON に <c>null</c> と書かれていたときは、読み込み（Repository）で空にする。</remarks>
    public List<CliCommandNode> Session { get; set; } = [];

    /// <summary>設定の誤り（知らない <c>switchTo</c> / <c>focus</c> の値）を探す</summary>
    /// <returns>ユーザーに見せる警告のメッセージ。誤りが無ければ空</returns>
    /// <remarks>
    /// 手で編集した JSON なので、知らない値は読み込みでは無視する（何もしない）。ただし、黙って無視すると、書き間違いに気づけないので、
    /// 読み込んだあとにこれで調べて、画面で知らせる。
    /// </remarks>
    public IReadOnlyList<string> Validate()
    {
        List<string> problems = [];
        CheckNodes(Shell, "シェル", problems);
        CheckNodes(Session, "AI セッション", problems);
        return problems;
    }

    /// <summary>ノードの木を調べて、誤りを足す</summary>
    /// <param name="nodes">調べるノード</param>
    /// <param name="path">ここまでの表示名のつながり（メッセージに出す）</param>
    /// <param name="problems">誤りのメッセージの足し先</param>
    private static void CheckNodes(List<CliCommandNode>? nodes, string path, List<string> problems)
    {
        foreach (var node in nodes ?? [])
        {
            var here = $"{path} › {node.Label}";
            if (!string.IsNullOrWhiteSpace(node.SwitchTo) && node.GetSwitchTo() is null)
            {
                problems.Add(InvalidValueMessage(here, "switchTo", node.SwitchTo, Enum.GetNames<CommandCategory>()));
            }
            if (!string.IsNullOrWhiteSpace(node.Focus) && node.GetFocus() is null)
            {
                problems.Add(InvalidValueMessage(here, "focus", node.Focus, Enum.GetNames<FocusTarget>()));
            }

            CheckNodes(node.Children, here, problems);
        }
    }

    /// <summary>誤りの値のメッセージを作る</summary>
    /// <param name="where">場所（表示名のつながり）</param>
    /// <param name="property">項目の名前</param>
    /// <param name="value">書かれていた値</param>
    /// <param name="allowed">使える値（列挙型の名前）</param>
    /// <returns>ユーザーに見せるメッセージ</returns>
    private static string InvalidValueMessage(string where, string property, string value, string[] allowed)
        => $"「{where}」の {property}「{value}」は使えません（{string.Join(" / ", allowed.Select(name => name.ToLowerInvariant()))} のどれかを書きます）。この設定は無視します。";
}
