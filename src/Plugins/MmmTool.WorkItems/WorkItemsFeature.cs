namespace MmmTool.WorkItems;

/// <summary>作業リストの、機能としての識別 (オン・オフのキーと表示名)</summary>
public static class WorkItemsFeature
{
    /// <summary>機能のキー (設定ファイルにも使う。変えると、保存済みのオン・オフが外れる)</summary>
    public const string Key = "WorkItems";

    /// <summary>設定ページに出す機能の名前</summary>
    public const string DisplayName = "作業リスト";
}
