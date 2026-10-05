namespace MmmTool.Core.Reminders;

/// <summary>リマインダー 1 件の対応状態の変更</summary>
/// <param name="BaseNo">リマインダー本体の番号 (<see cref="Reminder.No"/>)</param>
/// <param name="Date">対象日 (yyyyMMdd の整数)</param>
/// <param name="Status">対応状態</param>
public sealed record ReminderStateChange(int BaseNo, int Date, ReminderStatus Status);
