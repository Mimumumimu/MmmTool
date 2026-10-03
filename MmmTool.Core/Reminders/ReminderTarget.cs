namespace MmmTool.Core.Reminders;

/// <summary>ある日の対象になるリマインダーと、その日の対応状態</summary>
/// <param name="Reminder">リマインダー本体（複製）</param>
/// <param name="Status">その日の対応状態（別の日の状態は未対応として扱う）</param>
public sealed record ReminderTarget(Reminder Reminder, ReminderStatus Status);
