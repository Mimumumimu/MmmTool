namespace MmmTool.Reminders.Core;

/// <summary>まとめて追加で選ぶ何分前に鳴らすか</summary>
public static class ReminderBulkLead
{
    /// <summary>選べる分</summary>
    public static IReadOnlyList<int> Minutes { get; } = [0, 3, 5, 10, 15];

    /// <summary>最初に選んでいる分</summary>
    /// <remarks>会議の入り忘れを防ぐのが目的なので、開始より前に気づける 5 分前にする。</remarks>
    public const int DefaultMinutes = 5;
}
