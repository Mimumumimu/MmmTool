namespace MmmTool.Reminders.BulkInput;

/// <summary>
/// まとめて追加で選ぶ、何分前に鳴らすかの選択肢。
/// </summary>
/// <param name="Minutes">何分前か。0 は開始時刻ちょうど</param>
/// <param name="Name">画面に出す名前</param>
public sealed record ReminderBulkLeadOption(int Minutes, string Name);
