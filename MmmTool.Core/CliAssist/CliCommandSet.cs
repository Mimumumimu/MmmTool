namespace MmmTool.Core.CliAssist;

/// <summary>CLI補助の定型コマンド定義（Data/CliCommands.json）</summary>
/// <remarks>DB には載せないローカル専用の設定で、人が手で編集しやすいよう ID を持たない入れ子の形で保存する。</remarks>
public sealed class CliCommandSet
{
    /// <summary>シェルで打つコマンド（AI エージェント起動前）。</summary>
    public List<CliCommandNode>? Shell { get; set; } = [];

    /// <summary>AI エージェント（Claude Code・Kiro 等）のセッション内で打つコマンド（起動後）</summary>
    /// <remarks>ツールごとにフォルダで分ける。</remarks>
    public List<CliCommandNode>? Session { get; set; } = [];
}
