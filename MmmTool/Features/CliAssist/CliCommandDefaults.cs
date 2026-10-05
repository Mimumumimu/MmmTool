using MmmSdk.Core.Components.Shells;
using MmmTool.Core.CliAssist;

namespace MmmTool.Features.CliAssist;

/// <summary>
/// CliCommands.json が無いとき・初期化するときに作る既定の定型コマンド。
/// </summary>
/// <remarks>
/// アプリの配置 (<c>{AppDir}\Assets\Tools\</c> の補助スクリプト)を知っているので、Core ではなくアプリ側に置く。
/// 初期設定で選んだツールごとに、「シェル」「AI セッション」の両方のタブにフォルダを作る。ツールどうしで、同じ働きのコマンドは同じ表示名・同じ順にそろえる。
/// 環境で違うのは、Claude Code の会話履歴の削除の補助スクリプトだけ (ほかのコマンドは同じ)。
/// Windows では、補助スクリプトを動かすシェルを、ターミナルで使うシェル (<see cref="ShellLocator"/>)に合わせる (pwsh が無い環境では Windows PowerShell)。
/// WSL では、Python 版 (Ubuntu に標準で入っている python3 で動かす)を使う。WSL には Windows のごみ箱が無く、PowerShell 版では WSL 側の履歴を戻せる形で消せないため。
/// <c>{AppDir}</c> は、送るときに環境に合わせたパスへ展開する (WSL では <c>/mnt/d/...</c>)。
/// </remarks>
public static class CliCommandDefaults
{
    /// <summary>Kiro に steering ファイルを作ってもらう指示</summary>
    /// <remarks>Kiro の CLI には Claude Code の <c>/init</c> にあたるコマンドが無いので、Kiro の IDE の「Generate Steering Docs」と同じ 3 つのファイルを作るよう頼む。</remarks>
    private const string KiroSteeringPrompt =
        "このプロジェクトを調べて、Kiro の steering ファイルを .kiro/steering/ に作成してください。"
        + "product.md (プロダクトの目的・利用者・主な機能)、tech.md (技術スタック・よく使うビルドやテストのコマンド)、"
        + "structure.md (フォルダ構成・命名などの決まり)の 3 つです。";

    /// <summary>既定の定型コマンドを作る</summary>
    /// <param name="setup">初期設定 (環境・使うツール)</param>
    /// <returns>既定の定型コマンド</returns>
    public static CliCommandSet Create(CliSetup setup) => new()
    {
        Environment = CliCommandNode.ToJsonValue(setup.Environment),
        Shell = [.. setup.Tools.Select(tool => ShellGroup(tool, setup.Environment))],
        Session = [.. setup.Tools.Select(SessionGroup)],
    };

    /// <summary>シェルで打つコマンド (ツールの起動など)のフォルダを作る</summary>
    /// <param name="tool">ツール</param>
    /// <param name="environment">コマンドを動かす環境</param>
    /// <returns>ツールのフォルダ</returns>
    private static CliCommandNode ShellGroup(CliTool tool, CliEnvironment environment) => tool switch
    {
        CliTool.Kiro => Group("Kiro",
            Leaf("起動", "kiro-cli chat", switchTo: CommandCategory.Session, focus: FocusTarget.Input),
            Leaf("続きから再開", "kiro-cli chat --resume", switchTo: CommandCategory.Session, focus: FocusTarget.Input),
            Leaf("最新化", "kiro-cli update"),
            // 一覧から矢印キーで選ぶ画面なので、キー操作できるようターミナルへ
            Leaf("会話履歴の削除", RemoveSessionCommand("Remove-KiroSession", environment), focus: FocusTarget.Terminal)),
        _ => Group("Claude Code",
            // 起動したら、以降はセッション内のコマンドを使うので AI セッションのタブへ切り替え、すぐ指示を書けるよう送信欄へ
            Leaf("起動", "claude", switchTo: CommandCategory.Session, focus: FocusTarget.Input),
            // 直前の会話を引き継いで起動するので、起動と同じく AI セッションのタブ・送信欄へ
            Leaf("続きから再開", "claude --continue", switchTo: CommandCategory.Session, focus: FocusTarget.Input),
            Leaf("最新化", "claude update"),
            // 一覧から矢印キーで選ぶ画面なので、キー操作できるようターミナルへ
            Leaf("会話履歴の削除", RemoveSessionCommand("Remove-ClaudeSession", environment), focus: FocusTarget.Terminal)),
    };

    /// <summary>AI のセッション内で打つコマンドのフォルダを作る</summary>
    /// <param name="tool">ツール</param>
    /// <returns>ツールのフォルダ</returns>
    private static CliCommandNode SessionGroup(CliTool tool) => tool switch
    {
        CliTool.Kiro => Group("Kiro",
            Group("セッション内",
                // Kiro の /clear は同じ会話のまま中身を消すだけなので、新しい会話を始める /chat new を使う
                Leaf("新規チャット", "/chat new", focus: FocusTarget.Input),
                Leaf("読み込みファイル一覧", "/context"),
                Leaf("会話要約 (コンテキスト圧縮)", "/compact"),
                Leaf("steering を作成", KiroSteeringPrompt)),
            Leaf("モデル切替", "/model", focus: FocusTarget.Terminal),
            Leaf("会話履歴と再開", "/chat resume", focus: FocusTarget.Terminal),
            Leaf("使用量の確認", "/usage", focus: FocusTarget.Terminal),
            Leaf("終了", "/quit", switchTo: CommandCategory.Shell)),
        _ => Group("Claude Code",
            Group("セッション内",
                Leaf("新規チャット", "/clear", focus: FocusTarget.Input),
                Leaf("読み込みファイル一覧", "/context"),
                Leaf("会話要約 (コンテキスト圧縮)", "/compact"),
                Leaf("CLAUDE.md を作成", "/init")),
            // 一覧から選ぶ操作になるので、キー操作できるようターミナルへ
            Leaf("モデル切替", "/model", focus: FocusTarget.Terminal),
            Leaf("会話履歴と再開", "/resume", focus: FocusTarget.Terminal),
            Leaf("コスト確認", "/cost", focus: FocusTarget.Terminal),
            // 終了したらシェルに戻るので、シェルのタブへ切り替える
            Leaf("終了", "/exit", switchTo: CommandCategory.Shell)),
    };

    /// <summary>会話履歴の削除の補助スクリプトを動かすコマンドを作る</summary>
    /// <param name="scriptName">スクリプトの名前 (拡張子なし。<c>Assets/Tools/</c> の <c>.ps1</c> と <c>.py</c> の組)</param>
    /// <param name="environment">コマンドを動かす環境</param>
    /// <returns>ターミナルへ送るコマンド文字列</returns>
    private static string RemoveSessionCommand(string scriptName, CliEnvironment environment) => environment switch
    {
        CliEnvironment.Wsl => $"python3 \"{CommandPlaceholders.AppDir}/Assets/Tools/{scriptName}.py\"",
        _ => $"{ShellLocator.Default.FileName} -NoProfile -File \"{CommandPlaceholders.AppDir}\\Assets\\Tools\\{scriptName}.ps1\"",
    };

    /// <summary>フォルダ (子を持つノード)を作る</summary>
    /// <param name="label">表示名</param>
    /// <param name="children">子要素</param>
    /// <returns>フォルダのノード</returns>
    private static CliCommandNode Group(string label, params CliCommandNode[] children)
        => new() { Label = label, Children = [.. children] };

    /// <summary>コマンド (葉)を作る</summary>
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
