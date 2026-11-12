namespace MmmTool.WorkItems.Core;

/// <summary>日報メモ</summary>
/// <remarks>
/// 作業に結びつかない、その人の 1 日分の記入。
/// 1 人 1 日 1 件で、持ち主は今のユーザーで、保存先が決める。
/// 空にしたときは行を消さず、<see cref="Note"/> を空にする。
/// 作業報告では空 (空白だけも含む)と、行が無いことを区別しない。
/// 判定は <see cref="HasText"/> で行う。
/// </remarks>
public sealed record WorkDayNote
{
    /// <summary>番号</summary>
    /// <remarks>保存先が決める</remarks>
    public int Id { get; init; }

    /// <summary>持ち主のユーザーの番号</summary>
    /// <remarks>全員分を 1 回で読んだ作業報告を人ごとに分けるために持つ</remarks>
    public int UserId { get; init; }

    /// <summary>日付</summary>
    public DateOnly Date { get; init; }

    /// <summary>メモ</summary>
    public string Note { get; init; } = "";

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>メモに空白以外の文字があるか</summary>
    public bool HasText => !string.IsNullOrWhiteSpace(Note);
}
