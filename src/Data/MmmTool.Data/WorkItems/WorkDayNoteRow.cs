using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>
/// DB の <c>WorkDayNote</c> (日報メモ)の 1 行 (列と 1 対 1)。アプリの型 <see cref="WorkDayNote"/> とは別の、DB の形のまま持つ。
/// </summary>
/// <remarks>
/// 1 人 1 日 1 件。持ち主は業務の列 <see cref="UserId"/> で、<see cref="CreatedByUserId"/> は記録用 (検索・一意・結合には使わない)。
/// 作成・更新の日時と更新者は、Repository が入れる。
/// </remarks>
public sealed record WorkDayNoteRow
{
    /// <summary>主キー (<c>int</c>)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか (この表の操作では使わない。常に false)</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (ユーザーの番号。記録用)</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (ユーザーの番号)</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>持ち主 (<c>AppUser.Id</c>。ローカルモードは 1)</summary>
    public int UserId { get; init; }

    /// <summary>日付</summary>
    public DateOnly Date { get; init; }

    /// <summary>メモ (無いときは空文字)</summary>
    public string Note { get; init; } = "";

    /// <summary>メモの長さを検査する</summary>
    /// <param name="note">メモ</param>
    /// <returns>同じ文字列</returns>
    /// <exception cref="ArgumentException">長すぎる。</exception>
    public static string CheckNote(string note) => WorkRowValues.CheckText(note, "日報メモ", WorkRowValues.DayNoteMax);

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>日報メモ</returns>
    public WorkDayNote ToWorkDayNote() => new() { Id = Id, Date = Date, Note = Note, UpdatedAt = UpdatedAt };
}
