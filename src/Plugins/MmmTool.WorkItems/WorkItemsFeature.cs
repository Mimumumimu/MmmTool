namespace MmmTool.WorkItems;

/// <summary>作業管理の機能としての識別</summary>
public static class WorkItemsFeature
{
    /// <summary>機能のキー</summary>
    /// <remarks>設定ファイルにも使うので、変えると保存済みのオン・オフが外れる。</remarks>
    public const string Key = Core.WorkReportDayContentSource.WorkItemsFeatureKey;

    /// <summary>設定ページに出す機能の名前</summary>
    public const string DisplayName = "作業管理";
}
