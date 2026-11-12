namespace MmmTool.Shell;

/// <summary>設定ページの先頭に並べる部品の並び順の値</summary>
/// <remarks>
/// 機能に属さないもの。
/// 各機能の部品は機能の並び順の値 (0 以上)で並ぶ。
/// ここの値はすべて、それより前に並ぶ。
/// 「全般」→「保存先」→「アカウント」→「機能」の一覧 →「選択肢」→ 通知の 3 つの節 → 各機能の設定、の順になる。
/// </remarks>
public static class SettingsSectionOrder
{
    /// <summary>全般</summary>
    public const int General = -300;

    /// <summary>保存先</summary>
    public const int Database = -200;

    /// <summary>アカウントで、表示名・ログイン名・パスワードの変更とログアウト</summary>
    /// <remarks>DB に保存していて、ログイン済みのときだけ。</remarks>
    public const int Account = -150;

    /// <summary>機能の一覧</summary>
    public const int FeatureList = -100;

    /// <summary>選択肢で、ローカルモードのときだけ、コードマスタの名前を編集する</summary>
    public const int CodeMaster = -50;

    /// <summary>通知の節の先頭の「送信先」</summary>
    /// <remarks>
    /// 続く「通知先」「通知を受け取る機能」は 1 ずつ大きい値で、すぐあとに並ぶ。
    /// サーバーモードのときだけ。
    /// </remarks>
    public const int Notifications = -40;
}
