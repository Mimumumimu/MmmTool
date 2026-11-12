namespace MmmTool.WorkItems.Core;

/// <summary>スケジュールで、画面で選んだ状態</summary>
/// <param name="displaySettings">起動時の表示の設定</param>
/// <remarks>
/// ページを作り直しても引き継ぐためにページの外で持つ
/// 持ち方と持たないものは <see cref="WorkItemViewState"/> と同じ
/// </remarks>
public sealed class ScheduleViewState(WorkItemDisplaySettings displaySettings)
{
    /// <summary>表示の単位</summary>
    public ScheduleUnit Unit { get; set; } = ScheduleUnit.Day;

    /// <summary>「完了」の選択</summary>
    public CompletedDisplay Completed { get; set; } = displaySettings.ScheduleCompleted;

    /// <summary>利用者が閉じた案件・グループ</summary>
    public HashSet<WorkKey> Collapsed { get; } = [];
}
