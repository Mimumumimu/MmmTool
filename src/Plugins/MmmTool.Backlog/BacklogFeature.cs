namespace MmmTool.Backlog;

/// <summary>Backlog 連携の機能としての識別</summary>
public static class BacklogFeature
{
    /// <summary>機能のキー</summary>
    /// <remarks>設定ファイルにも使うので、変えると保存済みのオン・オフが外れる。</remarks>
    public const string Key = "Backlog";

    /// <summary>設定ページ・サイドバーに出す機能の名前</summary>
    public const string DisplayName = "Backlog";
}
