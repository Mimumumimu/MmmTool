namespace MmmTool.Reminders.BulkInput;

/// <summary>
/// まとめて追加の時刻の表の、1 時間ぶんの行。
/// </summary>
/// <param name="Hour">時 (0〜23)</param>
/// <param name="OnTheHour">ちょうどのマス (:00)</param>
/// <param name="HalfPast">30 分のマス (:30)</param>
public sealed record ReminderBulkHourRow(int Hour, ReminderBulkSlot OnTheHour, ReminderBulkSlot HalfPast)
{
    /// <summary>行の見出し</summary>
    public string Label => $"{Hour} 時";
}
