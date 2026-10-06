namespace MmmTool.Reminders.Core;

/// <summary>リマインダーの対応状態</summary>
public enum ReminderStatus
{
    /// <summary>未対応</summary>
    None = 0,
    /// <summary>完了</summary>
    Done = 1,
    /// <summary>スヌーズ</summary>
    Snooze = 2,
}
