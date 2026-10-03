namespace MmmTool.Core.CliAssist;

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

    /// <summary>作業ディレクトリを移動するコマンドを作る。</summary>
    /// <param name="kind">シェルの種類</param>
    /// <param name="directory">移動先のディレクトリ</param>
    /// <param name="command">シェルへ送るコマンド文字列（作れたとき）</param>
    /// <returns>作れたら true。cmd で、パスに <c>%</c> を含むときは false</returns>
    /// <remarks>
    /// cmd は、対話入力では引用符の中でも <c>%名前%</c> を環境変数に展開し、<c>%</c> を安全に打ち消す方法もない。
    /// 意図しないパスへ移動しないよう、cmd で <c>%</c> を含むパスは、コマンドを作らない。
    /// </remarks>
    public static bool TryChangeDirectory(ShellKind kind, string directory, out string command)
    {
        if (kind == ShellKind.Cmd)
        {
            command = directory.Contains('%') ? "" : $"cd /d \"{directory}\"";
            return command.Length > 0;
        }

        // 単一引用符なら $ や ` が展開されない。パス中の ' は '' でエスケープする
        command = $"Set-Location -LiteralPath '{directory.Replace("'", "''")}'";
        return true;
    }
}
