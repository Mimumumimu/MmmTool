namespace MmmTool.Core.CliAssist;

/// <summary>
/// 定型コマンドに書ける置き換え語を展開する。
/// </summary>
public static class CommandPlaceholders
{
    /// <summary>アプリの EXE があるフォルダ (末尾の \ なし)を表す置き換え語。</summary>
    public const string AppDir = "{AppDir}";

    /// <summary>コマンド中の置き換え語を展開する。</summary>
    /// <param name="command">定型コマンドの文字列</param>
    /// <param name="appDirectory">アプリの EXE があるフォルダ</param>
    /// <returns>置き換え語を展開したコマンド</returns>
    public static string Expand(string command, string appDirectory)
        => command.Replace(AppDir, appDirectory.TrimEnd('\\', '/'), StringComparison.Ordinal);
}
