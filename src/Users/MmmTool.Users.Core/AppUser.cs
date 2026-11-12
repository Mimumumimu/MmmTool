namespace MmmTool.Users.Core;

/// <summary>このアプリを使う人</summary>
/// <remarks>
/// 1 件が 1 人で、ログイン名とパスワードで特定する。
/// 値は作ったあとに書き換えず、変えるときは <c>with</c> で新しく作る。
/// 値は <c>init</c> で設定する。
/// 作成・更新の日時は DB だけが持ち、アプリの型には出さない。
/// パスワードのハッシュもこの型には持たせない。
/// ハッシュは <see cref="AppUserCredential"/> にある。
/// </remarks>
public sealed record AppUser
{
    /// <summary>表示名の最大文字数で、UTF-16 の 1 単位で数える</summary>
    /// <remarks>DB の列の長さと同じ</remarks>
    public const int DisplayNameMaxLength = 50;

    /// <summary>ログイン名の最大文字数で、UTF-16 の 1 単位で数える</summary>
    /// <remarks>DB の列の長さと同じ</remarks>
    public const int LoginNameMaxLength = 50;

    /// <summary>ログイン名に使えない文字があるときの画面に出す文言</summary>
    public const string InvalidLoginNameMessage = "ログイン名に使えない文字があります。半角英数字と記号だけで入力してください。";

    /// <summary>ログイン名として使える文字だけでできているか</summary>
    /// <param name="loginName">調べるログイン名 (前後の空白は呼び出し側で取り除く)</param>
    /// <returns>使えるなら true</returns>
    /// <remarks>
    /// 半角英数字と半角記号で、空白は不可。
    /// 空は false。
    /// 新規登録・変更・ログインの入力で使う。
    /// </remarks>
    public static bool IsValidLoginName(string loginName) =>
        loginName.Length > 0 && loginName.All(c => c is > ' ' and <= '~');

    /// <summary>番号で、ID</summary>
    /// <remarks>0 は未登録で、保存先が採番する。</remarks>
    public int Id { get; init; }

    /// <summary>表示名</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>ログイン名で、大文字と小文字は区別しない</summary>
    /// <remarks>削除されていないユーザーの間で一意</remarks>
    public string LoginName { get; init; } = "";

    /// <summary>使い始める日</summary>
    public DateOnly ValidFrom { get; init; }

    /// <summary>使い終わる日で、含む</summary>
    /// <remarks>終わりがなければ DateOnly.MaxValue</remarks>
    public DateOnly ValidTo { get; init; } = DateOnly.MaxValue;

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>指定の日に使える状態か</summary>
    /// <param name="today">調べる日</param>
    /// <returns>使えるなら true</returns>
    /// <remarks>削除されておらず、使い始める日から使い終わる日までの間。</remarks>
    public bool IsActiveOn(DateOnly today) => !IsDeleted && ValidFrom <= today && today <= ValidTo;
}
