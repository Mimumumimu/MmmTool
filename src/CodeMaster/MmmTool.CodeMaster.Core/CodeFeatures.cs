namespace MmmTool.CodeMaster.Core;

/// <summary>コードマスタの機能番号の一覧</summary>
/// <remarks>
/// 機能の名前や表示の順が変わっても、DB の意味が変わるため、番号は変えない。
/// 新しい機能は最後の番号の次を足してから使う。
/// 番号はサイドバーの初期の並びに合わせて付けた。
/// 並びを入れ替えても番号は変えない。
/// 0 はどの機能にも属さない共通の設定の番号で、機能には割り当てない。
/// 区分番号・コード番号の割り当てはその機能の Core が決める。
/// </remarks>
public static class CodeFeatures
{
    /// <summary>共通</summary>
    /// <remarks>
    /// どの機能にも属さない設定の機能番号
    /// ユーザーごとの設定だけが使う
    /// </remarks>
    public const int Common = 0;

    /// <summary>作業管理</summary>
    public const int WorkItems = 1;

    /// <summary>ノート</summary>
    /// <remarks>管理者が決める設定の機能番号に使う</remarks>
    public const int Notes = 2;

    /// <summary>CLI補助</summary>
    public const int CliAssist = 3;

    /// <summary>リマインダー</summary>
    /// <remarks>管理者が決める設定の機能番号に使う</remarks>
    public const int Reminders = 4;

    /// <summary>メッセージ</summary>
    /// <remarks>管理者が決める設定の機能番号に使う</remarks>
    public const int Messages = 5;

    /// <summary>リンク</summary>
    public const int Links = 6;

    /// <summary>クリップボード転送</summary>
    public const int ClipboardTransfer = 7;

    /// <summary>Backlog</summary>
    public const int Backlog = 8;

    /// <summary>ツール</summary>
    public const int Tools = 9;
}
