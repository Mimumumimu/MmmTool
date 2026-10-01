namespace MmmTool.Services.Terminal;

/// <summary>
/// 既定で起動するシェルを決める。PowerShell 7（pwsh）があればそれを、無ければ Windows PowerShell を使う。
/// </summary>
public static class DefaultShell
{
    public static string GetCommandLine() => IsOnPath("pwsh.exe") ? "pwsh.exe" : "powershell.exe";

    private static bool IsOnPath(string fileName)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return paths.Any(dir => !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir.Trim(), fileName)));
    }
}
