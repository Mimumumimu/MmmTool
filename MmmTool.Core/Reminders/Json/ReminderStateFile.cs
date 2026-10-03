namespace MmmTool.Core.Reminders.Json;

/// <summary>ReminderStates.json の中身</summary>
internal sealed class ReminderStateFile
{
    /// <summary>対応状態の一覧</summary>
    public List<ReminderState>? Items { get; set; } = [];
}
