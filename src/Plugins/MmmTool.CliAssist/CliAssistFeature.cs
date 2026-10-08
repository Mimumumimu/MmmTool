namespace MmmTool.CliAssist;

/// <summary>CLI補助の、機能としての識別 (オン・オフのキーと表示名)</summary>
public static class CliAssistFeature
{
    /// <summary>機能のキー (設定ファイルにも使う。変えると、保存済みのオン・オフが外れる)</summary>
    public const string Key = "CliAssist";

    /// <summary>設定ページに出す機能の名前</summary>
    public const string DisplayName = "CLI補助";
}
