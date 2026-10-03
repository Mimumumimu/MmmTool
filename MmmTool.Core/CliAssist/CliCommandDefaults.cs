namespace MmmTool.Core.CliAssist;

/// <summary>
/// CliCommands.json が無いときに作る既定の定型コマンド。
/// </summary>
public static class CliCommandDefaults
{
    /// <summary>switchTo（切り替え先のタブ）の値</summary>
    private static class Tab
    {
        /// <summary>ターミナルのタブ</summary>
        public const string Terminal = "terminal";
        /// <summary>AI セッションのタブ</summary>
        public const string Session = "session";
    }

    /// <summary>focus（送信後のフォーカス移動先）の値</summary>
    private static class Focus
    {
        /// <summary>ターミナル</summary>
        public const string Terminal = "terminal";
        /// <summary>送信欄</summary>
        public const string Input = "input";
    }

    /// <summary>既定の定型コマンドを作る</summary>
    /// <returns>既定の定型コマンド</returns>
    public static CliCommandSet Create() => new()
    {
        Terminal =
        [
            Group("Claude Code",
                // 起動したら、以降はセッション内のコマンドを使うので AI セッションのタブへ切り替え、すぐ指示を書けるよう送信欄へ
                Leaf("起動", "claude", switchTo: Tab.Session, focus: Focus.Input),
                // 直前の会話を引き継いで起動するので、起動と同じく AI セッションのタブ・送信欄へ
                Leaf("続きから再開", "claude --continue", switchTo: Tab.Session, focus: Focus.Input),
                Leaf("最新化", "claude update"),
                // 一覧から矢印キーで選ぶ画面なので、キー操作できるようターミナルへ
                Leaf("会話履歴の削除",
                    $"pwsh -NoProfile -File \"{CommandPlaceholders.AppDir}\\Assets\\Tools\\Remove-ClaudeSession.ps1\"",
                    focus: Focus.Terminal)),
        ],
        Session =
        [
            Group("Claude Code",
                Group("セッション内",
                    Leaf("新規チャット", "/clear", focus: Focus.Input),
                    Leaf("読み込みファイル一覧", "/context"),
                    Leaf("会話要約（コンテキスト圧縮）", "/compact"),
                    // 粒度は引数で指定する（medium は指摘を絞る、high は広く拾う）
                    Group("コードレビュー",
                        Leaf("標準", "/code-review medium"),
                        Leaf("詳細", "/code-review high")),
                    Leaf("CLAUDE.md を作成", "/init")),
                // 一覧から選ぶ操作になるので、キー操作できるようターミナルへ
                Leaf("モデル切替", "/model", focus: Focus.Terminal),
                Leaf("会話履歴と再開", "/resume", focus: Focus.Terminal),
                Leaf("コスト確認", "/cost", focus: Focus.Terminal),
                // 終了したらシェルに戻るので、ターミナルのタブへ切り替える
                Leaf("終了", "/exit", switchTo: Tab.Terminal)),
        ],
    };

    /// <summary>フォルダ（子を持つノード）を作る</summary>
    /// <param name="label">表示名</param>
    /// <param name="children">子要素</param>
    /// <returns>フォルダのノード</returns>
    private static CliCommandNode Group(string label, params CliCommandNode[] children)
        => new() { Label = label, Children = [.. children] };

    /// <summary>コマンド（葉）を作る</summary>
    /// <param name="label">表示名</param>
    /// <param name="command">ターミナルへ送るコマンド文字列</param>
    /// <param name="switchTo">送信後に切り替えるタブ。切り替えないなら null</param>
    /// <param name="focus">送信後にフォーカスを移す先。移さないなら null</param>
    /// <returns>コマンドのノード</returns>
    private static CliCommandNode Leaf(string label, string command, string? switchTo = null, string? focus = null)
        => new() { Label = label, Command = command, SwitchTo = switchTo, Focus = focus };
}
