namespace MmmTool.Core.CliAssist;

/// <summary>
/// 既定で起動するシェルを決める。PowerShell 7（pwsh）があればそれを、無ければ Windows PowerShell を使う。
/// </summary>
public static class DefaultShell
{
    /// <summary>既定のシェルの起動コマンドラインを返す</summary>
    /// <returns>pwsh.exe があればそれ、無ければ powershell.exe</returns>
    public static string GetCommandLine() => IsOnPath("pwsh.exe") ? "pwsh.exe" : "powershell.exe";

    /// <summary>PATH 上に実行ファイルがあるか</summary>
    /// <param name="fileName">実行ファイル名</param>
    /// <returns>あれば true</returns>
    private static bool IsOnPath(string fileName)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return paths.Any(dir => !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir.Trim(), fileName)));
    }
}
