namespace MmmTool.Users.Core;

/// <summary>
/// このアプリを使う人。1 件が 1 人 (ログイン名とパスワードで特定する)。
/// </summary>
/// <remarks>
/// 値は作ったあとに書き換えず、変えるときは <c>with</c> で新しく作る (<c>init</c>)。
/// 作成・更新の日時は、DB だけが持つ (アプリの型には出さない)。パスワードのハッシュも、この型には持たせない (<see cref="AppUserCredential"/>)。
/// </remarks>
public sealed record AppUser
{
    /// <summary>表示名の最大文字数 (UTF-16 の 1 単位で数える。DB の列の長さと同じ)</summary>
    public const int DisplayNameMaxLength = 50;

    /// <summary>ログイン名の最大文字数 (UTF-16 の 1 単位で数える。DB の列の長さと同じ)</summary>
    public const int LoginNameMaxLength = 50;

    /// <summary>ログイン名に使えない文字があるときの、画面に出す文言</summary>
    public const string InvalidLoginNameMessage = "ログイン名に使えない文字があります。半角英数字と記号だけで入力してください。";

    /// <summary>ログイン名として使える文字だけでできているか (半角英数字と半角記号。空白は不可。空は false)</summary>
    /// <param name="loginName">調べるログイン名 (前後の空白は呼び出し側で取り除く)</param>
    /// <returns>使えるなら true</returns>
    /// <remarks>新規登録・変更・ログインの入力で使う。</remarks>
    public static bool IsValidLoginName(string loginName) =>
        loginName.Length > 0 && loginName.All(c => c is > ' ' and <= '~');

    /// <summary>番号 (ID)。0 は未登録 (保存先が採番する)</summary>
    public int Id { get; init; }

    /// <summary>表示名</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>ログイン名 (大文字と小文字は区別しない。削除されていないユーザーの間で一意)</summary>
    public string LoginName { get; init; } = "";

    /// <summary>使い始める日</summary>
    public DateOnly ValidFrom { get; init; }

    /// <summary>使い終わる日 (含む)。終わりがなければ <see cref="DateOnly.MaxValue"/></summary>
    public DateOnly ValidTo { get; init; } = DateOnly.MaxValue;

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>指定の日に、使える状態か (削除されておらず、使い始める日から使い終わる日までの間)</summary>
    /// <param name="today">調べる日</param>
    /// <returns>使えるなら true</returns>
    public bool IsActiveOn(DateOnly today) => !IsDeleted && ValidFrom <= today && today <= ValidTo;
}
