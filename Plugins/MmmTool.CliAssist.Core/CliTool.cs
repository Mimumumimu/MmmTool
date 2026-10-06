namespace MmmTool.CliAssist.Core;

/// <summary>CLI補助で使う AI のコマンドラインツール</summary>
/// <remarks>既定の定型コマンドは、ツールごとのフォルダに分けて作る。並びはこの順。</remarks>
public enum CliTool
{
    /// <summary>Claude Code (<c>claude</c>)</summary>
    ClaudeCode,

    /// <summary>Kiro (<c>kiro-cli</c>)</summary>
    Kiro,
}
