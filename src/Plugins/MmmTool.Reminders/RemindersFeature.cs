namespace MmmTool.Reminders;

/// <summary>リマインダー の機能としての識別で、キーと表示名</summary>
/// <remarks>オフにできないので、サイドバー・設定の並び順だけに使う。</remarks>
public static class RemindersFeature
{
    /// <summary>機能のキー</summary>
    /// <remarks>設定ファイルの並び順にも使うので、変えると保存済みの並び順から外れる。</remarks>
    public const string Key = "Reminders";

    /// <summary>設定ページに出す機能の名前</summary>
    public const string DisplayName = "リマインダー";
}
