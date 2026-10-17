namespace MmmTool.Notes;

/// <summary>ノートの、機能としての識別 (オン・オフのキーと表示名)</summary>
public static class NotesFeature
{
    /// <summary>機能のキー (設定ファイルにも使う。変えると、保存済みのオン・オフが外れる)</summary>
    public const string Key = "Notes";

    /// <summary>設定ページ・サイドバーに出す機能の名前</summary>
    public const string DisplayName = "ノート";
}
