namespace MmmToolAdmin.Shell;

/// <summary>アプリの名前と、データ・ログの置き場所</summary>
/// <remarks>アプリの名前・フォルダー名は、ここ 1 か所に持つ。多重起動の防止・エラーのダイアログ・データ・ログで共通。</remarks>
public static class AppInfo
{
    /// <summary>アプリの名前</summary>
    public const string Name = "MmmToolAdmin";

    /// <summary>設定などの保存先フォルダー</summary>
    /// <remarks>EXE と同じ場所の <c>Data</c>。</remarks>
    public static string DataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>消しても作り直されるデータ (キャッシュ)の保存先フォルダー</summary>
    /// <remarks>EXE と同じ場所の <c>Cache</c>。<c>Data</c> を軽く保ち、バックアップや持ち運びの対象にしないために分ける。</remarks>
    public static string CacheDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Cache");

    /// <summary>エラーのログの保存先フォルダー</summary>
    /// <remarks><see cref="DataDirectory"/> の下の <c>Logs</c>。アプリが書くものは Data にまとめ、EXE の横にフォルダーを増やさない。</remarks>
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "Logs");
}
