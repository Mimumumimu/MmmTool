namespace MmmBatch.Shell;

/// <summary>アプリのアイコン</summary>
public static class AppIcon
{
    /// <summary>アイコンファイルのパス</summary>
    /// <remarks>
    /// EXE と同じ場所の <c>Assets\app.ico</c>
    /// ウィンドウのアイコンが実行時にこのファイルから読む
    /// </remarks>
    public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
}
