namespace MmmToolWeb.Server.Hosting;

/// <summary>守りに関わる出来事を記録するときのログの分類の名前</summary>
/// <remarks>
/// この分類だけ、ログのファイルに、通常の情報の段階から書く。
/// ほかの分類は警告以上だけを書く。
/// </remarks>
internal static class SecurityLog
{
    /// <summary>ログの分類の名前</summary>
    public const string Category = "MmmToolWeb.Security";
}
