namespace MmmTool.Notes.Core;

/// <summary>
/// ノート 1 件。期間 (開始日から終了日まで)・タイトル・本文・重要度・公開範囲を持つ。
/// </summary>
/// <remarks>
/// 値は作ったあとに書き換えず、変えるときは <c>with</c> で新しく作る (<c>init</c>)。
/// 書いた人は業務の列 <see cref="AuthorUserId"/> で持つ (共通ヘッダーの作成者は、検索の条件に使わない)。
/// </remarks>
public sealed record Note
{
    /// <summary>番号 (ID)。保存先が採番する</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>書いた人 (<c>AppUser.Id</c>。ローカルモードは 1)</summary>
    public int AuthorUserId { get; init; }

    /// <summary>書いた人だけに見えるか (false は全員に見える)。ローカルモードは常に false</summary>
    public bool IsPrivate { get; init; }

    /// <summary>開始日</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>終了日 (含む)。終わりがなければ <see cref="DateOnly.MaxValue"/> (無期限)</summary>
    public DateOnly EndDate { get; init; } = DateOnly.MaxValue;

    /// <summary>重要度</summary>
    public NotePriority Priority { get; init; } = NotePriority.Normal;

    /// <summary>タイトル</summary>
    public string Title { get; init; } = "";

    /// <summary>本文 (書いたとおりの文字。HTML のタグを含みうる)</summary>
    public string Body { get; init; } = "";

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (<c>AppUser.Id</c>)。カードに出すだけで、検索には使わない</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>終了日が無い (無期限)か</summary>
    public bool IsOpenEnded => EndDate == DateOnly.MaxValue;

    /// <summary>指定の日に有効か (開始日 ≤ その日 ≤ 終了日)</summary>
    /// <param name="day">調べる日</param>
    /// <returns>有効なら true</returns>
    public bool IsActiveOn(DateOnly day) => StartDate <= day && day <= EndDate;
}
