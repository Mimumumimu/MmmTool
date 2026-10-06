using MmmSdk.Core.Components.Notifications;

namespace MmmTool.Reminders.Core;

/// <summary>どのリマインダーを通知するかを判定する (時刻・タイマー・設定に依存しない純粋な判定)</summary>
public static class ReminderEvaluator
{
    /// <summary>読み上げる文で、件名どうしをつなぐ言葉 (2 件目から前に付く)</summary>
    private const string SpeechSeparator = "続いて。";

    /// <summary>発動済みのリマインダーから、今の分に通知する項目を決める</summary>
    /// <param name="triggered">今日の発動対象で、発動時刻を過ぎたもの (時刻 → 参照番号の順)と、今日の対応状態</param>
    /// <param name="minute">今の分 (秒以下を切り捨てた時刻)</param>
    /// <param name="lastSnoozeNotifiedMinute">前回スヌーズ分を通知した分。まだ無ければ null</param>
    /// <param name="snoozeIntervalMinutes">スヌーズの再通知間隔 (分)</param>
    /// <returns>通知する項目と、スヌーズ分を含むか、読み上げる文 (読み上げ対象が無ければ null)</returns>
    /// <remarks>
    /// 未対応 (None)は毎分通知する。完了 (Done)は通知しない。
    /// スヌーズ (Snooze)は、前回スヌーズ分を通知してから間隔以上たっていれば通知する (間隔は、リマインダーごとではなく全体で数える。前回が無ければ通知する)。
    /// </remarks>
    public static ReminderEvaluation Evaluate(
        IReadOnlyList<ReminderTarget> triggered, DateTime minute, DateTime? lastSnoozeNotifiedMinute, int snoozeIntervalMinutes)
    {
        var snoozeDue = lastSnoozeNotifiedMinute is not { } last || (minute - last).TotalMinutes >= snoozeIntervalMinutes;
        List<NotificationItem> items = [];
        List<string> speeches = [];
        var includesSnooze = false;

        foreach (var (reminder, status) in triggered)
        {
            switch (status)
            {
                case ReminderStatus.None:
                    Add(reminder);
                    break;
                case ReminderStatus.Snooze when snoozeDue:
                    Add(reminder);
                    includesSnooze = true;
                    break;
            }
        }

        return new ReminderEvaluation(items, includesSnooze, speeches.Count > 0 ? string.Join(SpeechSeparator, speeches) : null);

        // 通知する 1 件を、項目に加える (読み上げ対象なら、読み上げる文にも加える)
        void Add(Reminder reminder)
        {
            items.Add(ToItem(reminder));
            if (reminder.IsSpeak)
            {
                speeches.Add(ToSpeech(reminder));
            }
        }
    }

    /// <summary>リマインダーを通知の項目にする (件名と備考をテキスト、リンクがあればリンク先に)</summary>
    /// <param name="reminder">リマインダー</param>
    /// <returns>通知の項目</returns>
    /// <remarks>備考があれば「件名 (備考)」にする。</remarks>
    private static NotificationItem ToItem(Reminder reminder)
    {
        var text = string.IsNullOrWhiteSpace(reminder.Note) ? reminder.Title : $"{reminder.Title} ({reminder.Note})";
        return new NotificationItem(text, string.IsNullOrWhiteSpace(reminder.Link) ? null : reminder.Link);
    }

    /// <summary>リマインダーを読み上げる文にする</summary>
    /// <param name="reminder">リマインダー</param>
    /// <returns>「件名。備考。」(備考が無ければ「件名。」)</returns>
    private static string ToSpeech(Reminder reminder)
        => string.IsNullOrWhiteSpace(reminder.Note) ? $"{reminder.Title}。" : $"{reminder.Title}。{reminder.Note}。";
}
