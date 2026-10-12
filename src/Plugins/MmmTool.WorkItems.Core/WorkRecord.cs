namespace MmmTool.WorkItems.Core;

/// <summary>日ごとの記録 (作業 × 日付で 1 件)</summary>
/// <remarks>
/// 人は区別しない。担当者は 1 人なので、ほかの人が入れても代行入力として、同じ 1 件を直す。
/// 実績と日の備考が両方とも空・0 になったら、記録を論理削除する。同じ日に入れ直したときは、その行を使い回す。
/// </remarks>
public sealed record WorkRecord
{
    /// <summary>番号 (保存先が決める。0 は、まだ保存していない)</summary>
    public long Id { get; init; }

    /// <summary>属する案件の番号 (作業の案件。選んだ案件に絞って読むため、記録の行にも持つ)</summary>
    public int ProjectId { get; init; }

    /// <summary>作業の番号</summary>
    public int WorkItemId { get; init; }

    /// <summary>日付</summary>
    public DateOnly Date { get; init; }

    /// <summary>その日の実績 (時間。0 以上 24 以下)</summary>
    public double Hours { get; init; }

    /// <summary>その日の備考</summary>
    public string Note { get; init; } = "";

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時 (保存先の時計)</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>実績も備考も空か (空なら、記録を論理削除する)</summary>
    public bool IsEmpty => Hours <= 0 && Note.Length == 0;
}
