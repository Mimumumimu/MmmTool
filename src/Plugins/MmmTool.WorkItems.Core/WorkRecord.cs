namespace MmmTool.WorkItems.Core;

/// <summary>日ごとの記録 (作業 × 日付で 1 件)</summary>
/// <remarks>実績と日の備考が両方とも空・0 になったら、記録を消す (空の行を残さない)。</remarks>
public sealed record WorkRecord
{
    /// <summary>作業の番号</summary>
    public int WorkItemId { get; init; }

    /// <summary>日付</summary>
    public DateOnly Date { get; init; }

    /// <summary>その日の実績 (時間。0 以上 24 以下)</summary>
    public double Hours { get; init; }

    /// <summary>その日の備考</summary>
    public string Note { get; init; } = "";

    /// <summary>実績も備考も空か (空なら、記録を消す)</summary>
    public bool IsEmpty => Hours <= 0 && Note.Length == 0;
}
