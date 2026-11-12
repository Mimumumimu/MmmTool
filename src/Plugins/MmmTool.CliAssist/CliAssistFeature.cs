namespace MmmTool.CliAssist;

/// <summary>CLI補助の機能としての識別</summary>
public static class CliAssistFeature
{
    /// <summary>機能のキー</summary>
    /// <remarks>設定ファイルにも使うので、変えると保存済みのオン・オフが外れる。</remarks>
    public const string Key = "CliAssist";

    /// <summary>設定ページに出す機能の名前</summary>
    public const string DisplayName = "CLI補助";
}
