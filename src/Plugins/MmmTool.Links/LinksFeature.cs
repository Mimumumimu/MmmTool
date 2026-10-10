namespace MmmTool.Links;

/// <summary>リンク の、機能としての識別 (キーと表示名。オフにできないので、サイドバー・設定の並び順だけに使う)</summary>
public static class LinksFeature
{
    /// <summary>機能のキー (設定ファイルの並び順にも使う。変えると、保存済みの並び順から外れる)</summary>
    public const string Key = "Links";

    /// <summary>設定ページに出す機能の名前</summary>
    public const string DisplayName = "リンク";
}
