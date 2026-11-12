using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>DB の WorkDayNoteの 1 行で、列と 1 対 1 に対応する</summary>
/// <remarks>
/// アプリの型 WorkDayNote とは別の DB の形のまま持つ。
/// 1 人 1 日 1 件。
/// 持ち主は業務の列 <see cref="UserId"/> で、<see cref="CreatedByUserId"/> は記録用で、検索・一意・結合には使わない。
/// 作成・更新の日時と更新者は Repository が入れる。
/// </remarks>
public sealed record WorkDayNoteRow
{
    /// <summary>主キー</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているかで、この表の操作では使わない</summary>
    /// <remarks>常に false</remarks>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者で、ユーザーの番号</summary>
    /// <remarks>記録用</remarks>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>持ち主で、AppUser.Id</summary>
    /// <remarks>ローカルモードは 1</remarks>
    public int UserId { get; init; }

    /// <summary>日付</summary>
    public DateOnly Date { get; init; }

    /// <summary>メモ</summary>
    public string Note { get; init; } = "";

    /// <summary>メモの長さを検査する</summary>
    /// <param name="note">メモ</param>
    /// <returns>同じ文字列</returns>
    /// <exception cref="ArgumentException">長すぎる</exception>
    public static string CheckNote(string note) => WorkRowValues.CheckText(note, "日報メモ", WorkRowValues.DayNoteMax);

    /// <summary>DB の行からアプリの型にする</summary>
    /// <returns>日報メモ</returns>
    public WorkDayNote ToWorkDayNote() => new() { Id = Id, UserId = UserId, Date = Date, Note = Note, UpdatedAt = UpdatedAt };
}
