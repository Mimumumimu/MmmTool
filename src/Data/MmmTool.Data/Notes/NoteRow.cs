using MmmTool.Notes.Core;

namespace MmmTool.Data.Notes;

/// <summary>
/// DB の <c>Note</c> (ノート)の 1 行 (列と 1 対 1)。アプリの型 <see cref="Note"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>
/// 書いた人は業務の列 <see cref="AuthorUserId"/> で、<see cref="CreatedByUserId"/> は記録用 (検索・結合・索引には使わない)。
/// 終了日が無い (無期限)ときは <c>9999-12-31</c> (<see cref="DateOnly.MaxValue"/>)。NULL は使わない (理由は docs/specs/database.md)。
/// </remarks>
public sealed record NoteRow
{
    /// <summary>主キー (<c>int</c>)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (ユーザーの番号。記録用)</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (ユーザーの番号)。カードに出すだけで、検索には使わない</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>書いた人 (<c>AppUser.Id</c>。ローカルモードは 1)。「自分だけ」のノートが見える人を決める</summary>
    public int AuthorUserId { get; init; }

    /// <summary>書いた人だけに見えるか</summary>
    public bool IsPrivate { get; init; }

    /// <summary>開始日</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>終了日。無期限は <c>9999-12-31</c></summary>
    public DateOnly EndDate { get; init; } = DateOnly.MaxValue;

    /// <summary>実施日。未設定は <c>9999-12-31</c></summary>
    public DateOnly EventDate { get; init; } = DateOnly.MaxValue;

    /// <summary>重要度 (最低 = 1 … 最高 = 5)</summary>
    public byte Priority { get; init; }

    /// <summary>タイトル</summary>
    public string Title { get; init; } = "";

    /// <summary>本文 (書いたとおりの文字)</summary>
    public string Body { get; init; } = "";

    /// <summary>保存しようとする内容が正しいか調べる</summary>
    /// <param name="input">入力内容</param>
    /// <exception cref="ArgumentException">タイトルが空か長すぎる・重要度や期間が正しくない。</exception>
    public static void Validate(NoteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Validate() is { } error)
        {
            throw new ArgumentException(error, nameof(input));
        }
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>ノート</returns>
    /// <exception cref="InvalidOperationException">重要度が、定義にない値 (バグか、手で直した値)。</exception>
    public Note ToNote()
    {
        var priority = (NotePriority)Priority;
        if (!Enum.IsDefined(priority))
        {
            throw new InvalidOperationException($"ノート {Id} の重要度が正しくありません ({Priority})。");
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
            Priority = priority,
            Title = Title,
            Body = Body,
            UpdatedAt = UpdatedAt,
            UpdatedByUserId = UpdatedByUserId,
        };
    }
}
