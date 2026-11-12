using MmmBatch.ReminderSending.Core;
using MmmBatch.Sending;
using MmmTool.Reminders.Core;

namespace MmmBatch.ReminderSending;

/// <summary>今日の送信予定の一覧の 1 行</summary>
/// <param name="item">元の送信予定</param>
/// <remarks>
/// 一覧は読み直すたびに作り直すので、値は変わらない。
/// 送る先の値は持たない。
/// 秘密のため。
/// </remarks>
public sealed class ReminderPlanItemViewModel(ReminderSendPlanItem item)
{
    /// <summary>元の送信予定</summary>
    /// <remarks>読み直したときに変わった行だけ差し替えるための比較にも使う</remarks>
    public ReminderSendPlanItem Source { get; } = item;

    /// <summary>送る時刻</summary>
    /// <remarks>HH:mm</remarks>
    public string TimeText { get; } = ReminderDates.ToTime(item.Target.Reminder.Time) is { } time ? time.ToString("HH:mm") : "";

    /// <summary>件名</summary>
    public string Title { get; } = item.Target.Reminder.Title;

    /// <summary>送信先を登録した人の表示名</summary>
    public string OwnerName { get; } = item.Target.OwnerName;

    /// <summary>送信先</summary>
    public string ChannelText { get; } = SendDisplay.ChannelText(item.Target.Channel.Kind, item.Target.Channel.Name);

    /// <summary>状態</summary>
    public string StatusText { get; } = ReminderSendDisplay.PlanStatusText(item);

    /// <summary>右クリックのメニューの文字</summary>
    /// <remarks>まだ送っていないものは「今すぐ送る」、送ろうとしたものは「再送」。</remarks>
    public string ResendText { get; } = item.Status is null ? "今すぐ送る" : "再送";
}
