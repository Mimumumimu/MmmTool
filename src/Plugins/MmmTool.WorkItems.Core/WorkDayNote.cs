namespace MmmTool.WorkItems.Core;

/// <summary>日報メモ (作業に結びつかない、その人の 1 日分の記入)</summary>
/// <remarks>
/// 1 人 1 日 1 件で、持ち主は今のユーザー (保存先が決める)。空にしたときは、行を消さず、<see cref="Note"/> を空にする。
/// 作業報告では、空 (空白だけも含む)と、行が無いことを区別しない (<see cref="HasText"/>)。
/// </remarks>
public sealed record WorkDayNote
{
    /// <summary>番号 (保存先が決める)</summary>
    public int Id { get; init; }

    /// <summary>持ち主のユーザーの番号。全員分を 1 回で読んだ作業報告を、人ごとに分けるために持つ</summary>
    public int UserId { get; init; }

    /// <summary>日付</summary>
    public DateOnly Date { get; init; }

    /// <summary>メモ (空のときは空文字)</summary>
    public string Note { get; init; } = "";

    /// <summary>更新日時 (保存先の時計)</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>メモに、空白以外の文字があるか</summary>
    public bool HasText => !string.IsNullOrWhiteSpace(Note);
}
