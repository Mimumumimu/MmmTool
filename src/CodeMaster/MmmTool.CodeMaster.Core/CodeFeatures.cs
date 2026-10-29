namespace MmmTool.CodeMaster.Core;

/// <summary>コードマスタの機能番号の一覧 (割り当ての台帳)</summary>
/// <remarks>
/// 機能の名前や表示の順が変わっても、DB の意味が変わるため、番号は変えない。新しい機能は、最後の番号の次を足してから使う。
/// 番号は、サイドバーの初期の並びに合わせて付けた。並びを入れ替えても、番号は変えない。
/// 0 は、どの機能にも属さない共通の設定の番号で、機能には割り当てない。
/// 区分番号・コード番号の割り当ては、その機能の Core が決める。
/// </remarks>
public static class CodeFeatures
{
    /// <summary>共通。どの機能にも属さない設定 (通知の受け取り先など)の機能番号。ユーザーごとの設定 (<c>AppUserSetting</c>)だけが使う</summary>
    public const int Common = 0;

    /// <summary>作業管理</summary>
    public const int WorkItems = 1;

    /// <summary>ノート。管理者が決める設定 (<c>AppUserAdminSetting</c>)の機能番号に使う</summary>
    public const int Notes = 2;

    /// <summary>CLI補助</summary>
    public const int CliAssist = 3;

    /// <summary>リマインダー。管理者が決める設定 (<c>AppUserAdminSetting</c>)の機能番号に使う</summary>
    public const int Reminders = 4;

    /// <summary>メッセージ。管理者が決める設定 (<c>AppUserAdminSetting</c>)の機能番号に使う</summary>
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
