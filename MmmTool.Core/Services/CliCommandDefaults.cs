using MmmTool.Core.Entities;

namespace MmmTool.Core.Services;

/// <summary>
/// CliCommands.json が無いときに作る既定の定型コマンド。
/// </summary>
public static class CliCommandDefaults
{
    // switchTo（切り替え先のタブ）の値
    private static class Tab
    {
        public const string Terminal = "terminal";
        public const string Session = "session";
    }

    // focus（送信後のフォーカス移動先）の値
    private static class Focus
    {
        public const string Terminal = "terminal";
        public const string Input = "input";
    }

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

    private static CliCommandNode Group(string label, params CliCommandNode[] children)
        => new() { Label = label, Children = [.. children] };

    private static CliCommandNode Leaf(string label, string command, string? switchTo = null, string? focus = null)
        => new() { Label = label, Command = command, SwitchTo = switchTo, Focus = focus };
}
