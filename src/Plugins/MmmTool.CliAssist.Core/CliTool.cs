namespace MmmTool.CliAssist.Core;

/// <summary>CLI補助で使う AI のコマンドラインツール</summary>
/// <remarks>
/// 既定の定型コマンドはツールごとのフォルダに分けて作る
/// 並びはこの順
/// </remarks>
public enum CliTool
{
    /// <summary>Claude Code</summary>
    ClaudeCode,

    /// <summary>Kiro</summary>
    Kiro,
}

/// <summary>CliTool の拡張</summary>
public static class CliToolExtensions
{
    /// <summary>定型コマンドのツールのフォルダの表示名を返す</summary>
    /// <param name="tool">ツール</param>
    /// <returns>フォルダの表示名</returns>
    public static string GetLabel(this CliTool tool) => tool == CliTool.Kiro ? "Kiro" : "Claude Code";
}
