namespace MmmBatch.Shell;

/// <summary>アプリのアイコン (EXE・ウィンドウで共通)</summary>
public static class AppIcon
{
    /// <summary>アイコンファイル (.ico)のパス</summary>
    /// <remarks>EXE と同じ場所の <c>Assets\app.ico</c>。ウィンドウのアイコンが、実行時にこのファイルから読む。</remarks>
    public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
}
