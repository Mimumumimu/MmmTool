namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// 守りに関わる出来事 (ログイン状態の発行・登録やログインの拒否・登録用コードの発行)を記録するときの、ログの分類の名前。
/// </summary>
/// <remarks>
/// この分類だけ、ログのファイルに、通常の情報の段階から書く。ほかの分類は、警告以上だけを書く。
/// </remarks>
internal static class SecurityLog
{
    /// <summary>ログの分類の名前。</summary>
    public const string Category = "MmmToolWeb.Security";
}
