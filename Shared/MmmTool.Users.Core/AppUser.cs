namespace MmmTool.Users.Core;

/// <summary>
/// このアプリを使う人 (の 1 台)。1 件が、1 人の 1 台の PC (MAC アドレスで特定する)。
/// </summary>
/// <remarks>
/// 1 人が複数の PC を使うときは、台ごとに 1 件を登録する。値は作ったあとに書き換えず、変えるときは <c>with</c> で新しく作る (<c>init</c>)。
/// 作成・更新の日時は、DB だけが持つ (アプリの型には出さない)。
/// </remarks>
public sealed record AppUser
{
    /// <summary>表示名の最大文字数 (UTF-16 の 1 単位で数える。DB の列の長さと同じ)</summary>
    public const int DisplayNameMaxLength = 50;

    /// <summary>番号 (ID)。0 は未登録 (保存先が採番する)</summary>
    public int Id { get; init; }

    /// <summary>表示名</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>MAC アドレス (大文字の 16 進 12 桁・区切りなし)</summary>
    public string MacAddress { get; init; } = "";

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
