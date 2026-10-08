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

/// <summary><see cref="CliTool"/> の拡張</summary>
public static class CliToolExtensions
{
    /// <summary>定型コマンドのツールのフォルダの表示名を返す</summary>
    /// <param name="tool">ツール</param>
    /// <returns>フォルダの表示名</returns>
    public static string GetLabel(this CliTool tool) => tool == CliTool.Kiro ? "Kiro" : "Claude Code";
}
