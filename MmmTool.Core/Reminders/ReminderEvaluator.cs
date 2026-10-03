using MmmSdk.Core.Components.Notifications;

namespace MmmTool.Core.Reminders;

/// <summary>どのリマインダーを通知するかを判定する（時刻・タイマー・設定に依存しない純粋な判定）</summary>
public static class ReminderEvaluator
{
    /// <summary>発動済みのリマインダーから、今の分に通知する項目を決める</summary>
    /// <param name="triggered">今日の発動対象で、発動時刻を過ぎたもの（時刻 → 参照番号の順）と、今日の対応状態</param>
    /// <param name="minute">今の分（秒以下を切り捨てた時刻）</param>
    /// <param name="lastSnoozeNotifiedMinute">前回スヌーズ分を通知した分。まだ無ければ null</param>
    /// <param name="snoozeIntervalMinutes">スヌーズの再通知間隔（分）</param>
    /// <returns>通知する項目と、スヌーズ分を含むか</returns>
    /// <remarks>
    /// 未対応（None）は毎分通知する。完了（Done）は通知しない。
    /// スヌーズ（Snooze）は、前回スヌーズ分を通知してから間隔以上たっていれば通知する（間隔は、リマインダーごとではなく全体で数える。前回が無ければ通知する）。
    /// </remarks>
    public static ReminderEvaluation Evaluate(
        IReadOnlyList<ReminderTarget> triggered, DateTime minute, DateTime? lastSnoozeNotifiedMinute, int snoozeIntervalMinutes)
    {
        var snoozeDue = lastSnoozeNotifiedMinute is not { } last || (minute - last).TotalMinutes >= snoozeIntervalMinutes;
        List<NotificationItem> items = [];
        var includesSnooze = false;

        foreach (var (reminder, status) in triggered)
        {
            switch (status)
            {
                case ReminderStatus.None:
                    items.Add(ToItem(reminder));
                    break;
                case ReminderStatus.Snooze when snoozeDue:
                    items.Add(ToItem(reminder));
                    includesSnooze = true;
                    break;
            }
        }

        return new ReminderEvaluation(items, includesSnooze);
    }

    /// <summary>リマインダーを通知の項目にする（件名をテキスト、リンクがあればリンク先に）</summary>
    /// <param name="reminder">リマインダー</param>
    /// <returns>通知の項目</returns>
    private static NotificationItem ToItem(Reminder reminder)
        => new(reminder.Title, string.IsNullOrWhiteSpace(reminder.Link) ? null : reminder.Link);
}
