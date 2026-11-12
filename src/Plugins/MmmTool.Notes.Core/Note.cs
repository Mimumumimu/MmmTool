namespace MmmTool.Notes.Core;

/// <summary>ノート 1 件</summary>
/// <remarks>
/// 期間・実施日・ほかの実施日・タイトル・本文・重要度・公開範囲を持つ。
/// 値は作ったあとに書き換えず、変えるときは <c>with</c> で新しく作る。
/// 値は <c>init</c> で設定する。
/// 書いた人は業務の列 <see cref="AuthorUserId"/> で持つ。
/// 共通ヘッダーの作成者は検索の条件に使わない。
/// </remarks>
public sealed record Note
{
    /// <summary>番号で、ID</summary>
    /// <remarks>保存先が採番する</remarks>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>書いた人で、AppUser.Id</summary>
    /// <remarks>ローカルモードは 1</remarks>
    public int AuthorUserId { get; init; }

    /// <summary>書いた人だけに見えるか</summary>
    /// <remarks>
    /// false は全員に見える
    /// ローカルモードは常に false
    /// </remarks>
    public bool IsPrivate { get; init; }

    /// <summary>開始日</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>終了日で、含む</summary>
    /// <remarks>終わりがなければ DateOnly.MaxValue</remarks>
    public DateOnly EndDate { get; init; } = DateOnly.MaxValue;

    /// <summary>実施日で、ノート全体の日</summary>
    /// <remarks>未設定なら DateOnly.MaxValue</remarks>
    public DateOnly EventDate { get; init; } = DateOnly.MaxValue;

    /// <summary>ほかの実施日</summary>
    /// <remarks>
    /// 日付の順に並び、実施日と同じ日・同じ日の重なりは含まない。
    /// 無ければ空。
    /// </remarks>
    public IReadOnlyList<DateOnly> OtherEventDates { get; init; } = [];

    /// <summary>重要度</summary>
    public NotePriority Priority { get; init; } = NotePriority.Normal;

    /// <summary>タイトル</summary>
    public string Title { get; init; } = "";

    /// <summary>本文で、書いたとおりの文字</summary>
    /// <remarks>HTML のタグを含みうる</remarks>
    public string Body { get; init; } = "";

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者で、AppUser.Id の値</summary>
    /// <remarks>カードに出すだけで、検索には使わない。</remarks>
    public int UpdatedByUserId { get; init; }

    /// <summary>終了日が無いか</summary>
    public bool IsOpenEnded => EndDate == DateOnly.MaxValue;

    /// <summary>実施日が設定されているか</summary>
    public bool HasEventDate => EventDate != DateOnly.MaxValue;

    /// <summary>指定の日が実施日かほかの実施日のどれかか</summary>
    /// <param name="day">調べる日</param>
    /// <returns>実施日かほかの実施日のどれかがその日なら true</returns>
    public bool IsEventOn(DateOnly day) => (HasEventDate && EventDate == day) || OtherEventDates.Contains(day);

    /// <summary>指定の日より後で、いちばん近い実施日を取得する</summary>
    /// <param name="day">基準の日</param>
    /// <returns>
    /// 基準の日より後で、いちばん近い日。
    /// 無ければ null。
    /// </returns>
    /// <remarks>実施日とほかの実施日を合わせて探す</remarks>
    public DateOnly? NextEventAfter(DateOnly day) => NoteEventDates.NextAfter(EventDate, OtherEventDates, day);

    /// <summary>指定の日に有効か</summary>
    /// <param name="day">調べる日</param>
    /// <returns>有効なら true</returns>
    public bool IsActiveOn(DateOnly day) => StartDate <= day && day <= EndDate;
}
