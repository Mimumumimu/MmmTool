using MmmTool.Notes.Core;

namespace MmmTool.Data.Notes;

/// <summary>DB の Noteの 1 行で、列と 1 対 1 に対応する</summary>
/// <remarks>
/// アプリの型 Note とは別の DB の形のまま持つ。
/// 書いた人は業務の列 <see cref="AuthorUserId"/> で、<see cref="CreatedByUserId"/> は記録用で、検索・結合・索引には使わない。
/// 終了日が無い (無期限)ときは <c>9999-12-31</c>、つまり <see cref="DateOnly.MaxValue"/>。
/// NULL は使わない。
/// 理由は docs/specs/database.md にある。
/// </remarks>
public sealed record NoteRow
{
    /// <summary>主キー</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者で、ユーザーの番号</summary>
    /// <remarks>記録用</remarks>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者で、ユーザーの番号</summary>
    /// <remarks>カードに出すだけで、検索には使わない。</remarks>
    public int UpdatedByUserId { get; init; }

    /// <summary>書いた人で、AppUser.Id の値</summary>
    /// <remarks>
    /// ローカルモードは 1
    /// 「自分だけ」のノートが見える人を決める
    /// </remarks>
    public int AuthorUserId { get; init; }

    /// <summary>書いた人だけに見えるか</summary>
    public bool IsPrivate { get; init; }

    /// <summary>開始日</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>終了日</summary>
    /// <remarks>無期限は 9999-12-31</remarks>
    public DateOnly EndDate { get; init; } = DateOnly.MaxValue;

    /// <summary>実施日</summary>
    /// <remarks>未設定は 9999-12-31</remarks>
    public DateOnly EventDate { get; init; } = DateOnly.MaxValue;

    /// <summary>重要度</summary>
    /// <remarks>最低 = 1 … 最高 = 5</remarks>
    public byte Priority { get; init; }

    /// <summary>タイトル</summary>
    public string Title { get; init; } = "";

    /// <summary>本文</summary>
    public string Body { get; init; } = "";

    /// <summary>保存しようとする内容が正しいか調べる</summary>
    /// <param name="input">入力内容</param>
    /// <exception cref="ArgumentException">タイトルが空か長すぎる・重要度や期間が正しくない</exception>
    public static void Validate(NoteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Validate() is { } error)
        {
            throw new ArgumentException(error, nameof(input));
        }
    }

    /// <summary>DB の行からアプリの型にする</summary>
    /// <param name="otherEventDates">
    /// ほかの実施日で、<c>NoteAttribute</c> の行から読んだ日付の順の一覧。
    /// 省くと、無し。
    /// </param>
    /// <returns>ノート</returns>
    /// <exception cref="InvalidOperationException">重要度が定義にない値で、バグか、手で直した値。</exception>
    public Note ToNote(IReadOnlyList<DateOnly>? otherEventDates = null)
    {
        var priority = (NotePriority)Priority;
        if (!Enum.IsDefined(priority))
        {
            throw new InvalidOperationException($"ノート {Id} の重要度が正しくありません。値は {Priority} です。");
        }

        return new Note
        {
            Id = Id,
            IsDeleted = IsDeleted,
            AuthorUserId = AuthorUserId,
            IsPrivate = IsPrivate,
            StartDate = StartDate,
            EndDate = EndDate,
            EventDate = EventDate,
            OtherEventDates = otherEventDates ?? [],
            Priority = priority,
            Title = Title,
            Body = Body,
            UpdatedAt = UpdatedAt,
            UpdatedByUserId = UpdatedByUserId,
        };
    }
}
