namespace MmmTool.Core.Services;

/// <summary>シェルの種類</summary>
public enum ShellKind
{
    /// <summary>PowerShell</summary>
    PowerShell,
    /// <summary>コマンドプロンプト（cmd）</summary>
    Cmd,
}

/// <summary>
/// シェルの種類に合わせたコマンド文字列を組み立てる。
/// </summary>
public static class ShellCommands
{
    /// <summary>起動コマンドラインからシェルの種類を判定する</summary>
    /// <param name="commandLine">シェルの起動コマンドライン</param>
    /// <returns>シェルの種類</returns>
    /// <remarks>cmd 以外は PowerShell 扱い。</remarks>
    public static ShellKind DetectKind(string commandLine)
    {
        var trimmed = commandLine.TrimStart();
        var executable = trimmed.StartsWith('"')
            ? trimmed[1..].Split('"')[0]
            : trimmed.Split(' ')[0];

        return Path.GetFileNameWithoutExtension(executable).Equals("cmd", StringComparison.OrdinalIgnoreCase)
            ? ShellKind.Cmd
            : ShellKind.PowerShell;
    }

    /// <summary>作業ディレクトリを移動するコマンド。</summary>
    /// <param name="kind">シェルの種類</param>
    /// <param name="directory">移動先のディレクトリ</param>
    /// <returns>シェルへ送るコマンド文字列</returns>
    public static string ChangeDirectory(ShellKind kind, string directory) => kind switch
    {
        ShellKind.Cmd => $"cd /d \"{directory}\"",
        // 単一引用符なら $ や ` が展開されない。パス中の ' は '' でエスケープする
        _ => $"Set-Location -LiteralPath '{directory.Replace("'", "''")}'",
    };
}
