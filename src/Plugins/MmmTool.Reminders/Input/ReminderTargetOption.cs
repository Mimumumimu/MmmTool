namespace MmmTool.Reminders.Input;

/// <summary>
/// 入力画面で選ぶ、リマインダーの宛先 (選択肢)。
/// </summary>
/// <param name="UserId">宛先のユーザー (<c>AppUser.Id</c>)。0 は全員宛て</param>
/// <param name="Name">画面に出す名前</param>
public sealed record ReminderTargetOption(int UserId, string Name);
