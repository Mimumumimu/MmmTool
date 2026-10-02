using MmmTool.Core.Entities;

namespace MmmTool.Core.Services;

/// <summary>
/// CliCommands.json が無いときに作る既定の定型コマンド。
/// </summary>
public static class CliCommandDefaults
{
    private const string TerminalTab = "terminal";
    private const string SessionTab = "session";

    private const string FocusTerminal = "terminal";
    private const string FocusInput = "input";

    public static CliCommandSet Create() => new()
    {
        Terminal =
        [
            Group("Claude Code",
                // 起動したら、以降はセッション内のコマンドを使うので AI セッションのタブへ切り替え、すぐ指示を書けるよう送信欄へ
                Leaf("起動", "claude", switchTo: SessionTab, focus: FocusInput),
                Leaf("最新化", "claude update")),
        ],
        Session =
        [
            Group("Claude Code",
                Group("セッション内",
                    Leaf("新規チャット", "/clear", focus: FocusInput),
                    Leaf("読み込みファイル一覧", "/context"),
                    Leaf("会話要約（コンテキスト圧縮）", "/compact")),
                // 一覧から選ぶ操作になるので、キー操作できるようターミナルへ
                Leaf("モデル切替", "/model", focus: FocusTerminal),
                Leaf("会話履歴と再開", "/resume", focus: FocusTerminal),
                // 終了したらシェルに戻るので、ターミナルのタブへ切り替える
                Leaf("終了", "/exit", switchTo: TerminalTab)),
        ],
    };

    private static CliCommandNode Group(string label, params CliCommandNode[] children)
        => new() { Label = label, Children = [.. children] };

    private static CliCommandNode Leaf(string label, string command, string? switchTo = null, string? focus = null)
        => new() { Label = label, Command = command, SwitchTo = switchTo, Focus = focus };
}
