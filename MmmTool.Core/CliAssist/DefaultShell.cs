namespace MmmTool.Core.CliAssist;

/// <summary>
/// 既定で起動するシェルを決める。PowerShell 7（pwsh）があればそれを、無ければ Windows PowerShell を使う。
/// </summary>
public static class DefaultShell
{
    /// <summary>PowerShell 7 の実行ファイル名</summary>
    private const string PwshFileName = "pwsh.exe";

    /// <summary>Windows PowerShell の実行ファイル名</summary>
    private const string WindowsPowerShellFileName = "powershell.exe";

    /// <summary>既定のシェルを起動するコマンドラインを返す</summary>
    /// <returns>実行ファイルのフルパスを引用符で囲んだもの</returns>
    /// <remarks>
    /// 名前だけで起動すると、Windows が実行ファイルを探す場所（アプリのフォルダー・カレントフォルダーなど）に同名のファイルがあれば、それが起動してしまう。
    /// フルパスにして、起動するファイルを決めておく。
    /// </remarks>
    public static string GetCommandLine() => $"\"{GetExecutablePath()}\"";

    /// <summary>既定のシェルの実行ファイル名を返す</summary>
    /// <returns>pwsh.exe があればそれ、無ければ powershell.exe</returns>
    /// <remarks>シェルの中で、そのシェル自身を呼ぶコマンドに使う（シェルが自分で PATH から探すので、フルパスは要らない）。</remarks>
    public static string GetFileName() => FindOnPath(PwshFileName) is null ? WindowsPowerShellFileName : PwshFileName;

    /// <summary>既定のシェルの実行ファイルのフルパスを返す</summary>
    /// <returns>PATH 上の pwsh.exe があればそれ、無ければ Windows PowerShell（システムフォルダー内）</returns>
    private static string GetExecutablePath()
        => FindOnPath(PwshFileName) ?? Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", WindowsPowerShellFileName);

    /// <summary>PATH 上の実行ファイルを探す</summary>
    /// <param name="fileName">実行ファイル名</param>
    /// <returns>最初に見つかったもののフルパス。無ければ null</returns>
    private static string? FindOnPath(string fileName)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return paths
            .Where(dir => !string.IsNullOrWhiteSpace(dir))
            .Select(dir => Path.Combine(dir.Trim(), fileName))
            .FirstOrDefault(File.Exists);
    }
}
