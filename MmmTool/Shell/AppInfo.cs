namespace MmmTool.Shell;

/// <summary>アプリの名前と、データ・ログの置き場所</summary>
/// <remarks>アプリの名前・フォルダー名は、ここ 1 か所に持つ (多重起動の防止・エラーのダイアログ・トレイ・添付の一時フォルダー・データ・ログで共通)。</remarks>
public static class AppInfo
{
    /// <summary>アプリの名前</summary>
    public const string Name = "MmmTool";

    /// <summary>JSON の保存先フォルダー</summary>
    /// <remarks>EXE と同じ場所の <c>Data</c>。</remarks>
    public static string DataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>エラーのログの保存先フォルダー</summary>
    /// <remarks><see cref="DataDirectory"/> の下の <c>Logs</c>(アプリが書くものは Data にまとめ、EXE の横にフォルダーを増やさない)。</remarks>
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "Logs");

    /// <summary>トレイのウィンドウクラス名</summary>
    /// <remarks>別のアプリと重ならないよう、アプリの名前から作る。</remarks>
    public static string TrayWindowClassName { get; } = $"{Name}_Tray";
}
