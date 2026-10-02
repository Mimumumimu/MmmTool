namespace MmmTool.Core.Entities;

/// <summary>
/// CLI補助の定型コマンド定義（Data/CliCommands.json）。
/// DB には載せないローカル専用の設定で、人が手で編集しやすいよう ID を持たない入れ子の形で保存する。
/// </summary>
public sealed class CliCommandSet
{
    /// <summary>シェルで打つコマンド（AI エージェント起動前）。</summary>
    public List<CliCommandNode>? Terminal { get; set; } = [];

    /// <summary>AI エージェント（Claude Code・Kiro 等）のセッション内で打つコマンド（起動後）。ツールごとにフォルダで分ける。</summary>
    public List<CliCommandNode>? Session { get; set; } = [];
}

/// <summary>
/// 定型コマンドツリーの 1 要素。子を持てば中間ノード、コマンドを持てば葉。
/// </summary>
public sealed class CliCommandNode
{
    public string Label { get; set; } = "";

    /// <summary>ターミナルへ送るコマンド文字列（葉のとき）。</summary>
    public string? Command { get; set; }

    /// <summary>コマンドを送ったあとに切り替える左ペインのタブ（"terminal" または "session"）。省略すると切り替えない。</summary>
    public string? SwitchTo { get; set; }

    /// <summary>コマンドを送ったあとにフォーカスを移す先（"terminal" または "input"＝送信欄）。省略すると移さない。</summary>
    public string? Focus { get; set; }

    /// <summary>子要素（中間ノードのとき）。</summary>
    public List<CliCommandNode>? Children { get; set; }
}
