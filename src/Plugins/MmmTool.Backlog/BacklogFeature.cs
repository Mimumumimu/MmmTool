namespace MmmTool.Backlog;

/// <summary>Backlog 連携の、機能としての識別 (オン・オフのキーと表示名)</summary>
public static class BacklogFeature
{
    /// <summary>機能のキー (設定ファイルにも使う。変えると、保存済みのオン・オフが外れる)</summary>
    public const string Key = "Backlog";

    /// <summary>設定ページ・サイドバーに出す機能の名前</summary>
    public const string DisplayName = "Backlog";
}
