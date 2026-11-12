namespace MmmTool.ClipboardTransfer;

/// <summary>クリップボード転送の機能としての識別</summary>
public static class ClipboardTransferFeature
{
    /// <summary>機能のキー</summary>
    /// <remarks>設定ファイルにも使うので、変えると保存済みのオン・オフが外れる。</remarks>
    public const string Key = "ClipboardTransfer";

    /// <summary>設定ページ・サイドバーに出す機能の名前</summary>
    public const string DisplayName = "クリップボード転送";
}
