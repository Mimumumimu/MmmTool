namespace MmmTool.Core.Services;

/// <summary>
/// 定型コマンドに書ける置き換え語を展開する。
/// </summary>
public static class CommandPlaceholders
{
    /// <summary>アプリの EXE があるフォルダ（末尾の \ なし）を表す置き換え語。</summary>
    public const string AppDir = "{AppDir}";

    /// <summary>コマンド中の置き換え語を展開する。</summary>
    public static string Expand(string command, string appDirectory)
        => command.Replace(AppDir, appDirectory.TrimEnd('\\', '/'), StringComparison.Ordinal);
}
