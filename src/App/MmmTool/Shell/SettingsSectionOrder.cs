namespace MmmTool.Shell;

/// <summary>設定ページの先頭に並べる部品 (機能に属さないもの)の並び順の値</summary>
/// <remarks>
/// 各機能の部品は、機能の並び順の値 (0 以上)で並ぶ。ここの値は、すべて、それより前に並ぶ。
/// 「全般」→「保存先」→「機能」の一覧 → 各機能の設定、の順になる。
/// </remarks>
public static class SettingsSectionOrder
{
    /// <summary>全般 (メインウィンドウの設定など)</summary>
    public const int General = -300;

    /// <summary>保存先 (ローカル / DB)</summary>
    public const int Database = -200;

    /// <summary>機能の一覧 (オン・オフ)</summary>
    public const int FeatureList = -100;
}
