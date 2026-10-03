using MmmSdk.Core.Components.Shells;
using MmmTool.Core.CliAssist;

namespace MmmTool.Features.CliAssist;

/// <summary>
/// CliCommands.json が無いときに作る既定の定型コマンド。
/// </summary>
/// <remarks>
/// アプリの配置（<c>{AppDir}\Assets\Tools\</c> の補助スクリプト）を知っているので、Core ではなくアプリ側に置く。
/// 補助スクリプトを動かすシェルは、ターミナルで使うシェル（<see cref="ShellLocator"/>）に合わせる（pwsh が無い環境では Windows PowerShell）。
/// </remarks>
public static class CliCommandDefaults
{
    /// <summary>既定の定型コマンドを作る</summary>
    /// <returns>既定の定型コマンド</returns>
    public static CliCommandSet Create() => new()
    {
        Shell =
        [
            Group("Claude Code",
                // 起動したら、以降はセッション内のコマンドを使うので AI セッションのタブへ切り替え、すぐ指示を書けるよう送信欄へ
                Leaf("起動", "claude", switchTo: CommandCategory.Session, focus: FocusTarget.Input),
                // 直前の会話を引き継いで起動するので、起動と同じく AI セッションのタブ・送信欄へ
                Leaf("続きから再開", "claude --continue", switchTo: CommandCategory.Session, focus: FocusTarget.Input),
                Leaf("最新化", "claude update"),
                // 一覧から矢印キーで選ぶ画面なので、キー操作できるようターミナルへ
                Leaf("会話履歴の削除",
                    $"{ShellLocator.Default.FileName} -NoProfile -File \"{CommandPlaceholders.AppDir}\\Assets\\Tools\\Remove-ClaudeSession.ps1\"",
                    focus: FocusTarget.Terminal)),
        ],
        Session =
        [
            Group("Claude Code",
                Group("セッション内",
                    Leaf("新規チャット", "/clear", focus: FocusTarget.Input),
                    Leaf("読み込みファイル一覧", "/context"),
                    Leaf("会話要約（コンテキスト圧縮）", "/compact"),
                    Leaf("CLAUDE.md を作成", "/init")),
                // 一覧から選ぶ操作になるので、キー操作できるようターミナルへ
                Leaf("モデル切替", "/model", focus: FocusTarget.Terminal),
                Leaf("会話履歴と再開", "/resume", focus: FocusTarget.Terminal),
                Leaf("コスト確認", "/cost", focus: FocusTarget.Terminal),
                // 終了したらシェルに戻るので、シェルのタブへ切り替える
                Leaf("終了", "/exit", switchTo: CommandCategory.Shell)),
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
    private static CliCommandNode Leaf(string label, string command, CommandCategory? switchTo = null, FocusTarget? focus = null)
        => new()
        {
            Label = label,
            Command = command,
            SwitchTo = switchTo is { } tab ? CliCommandNode.ToJsonValue(tab) : null,
            Focus = focus is { } target ? CliCommandNode.ToJsonValue(target) : null,
        };
}
