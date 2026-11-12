namespace MmmTool.Reminders.Input;

/// <summary>入力画面で選ぶリマインダーの宛先で選択肢として使う</summary>
/// <param name="UserId">
/// 宛先のユーザーで、<c>AppUser.Id</c> の値。
/// 0 は全員宛て。
/// </param>
/// <param name="Name">画面に出す名前</param>
public sealed record ReminderTargetOption(int UserId, string Name);
