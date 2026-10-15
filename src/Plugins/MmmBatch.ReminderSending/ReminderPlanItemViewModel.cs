using MmmBatch.ReminderSending.Core;
using MmmBatch.Sending;
using MmmTool.Reminders.Core;

namespace MmmBatch.ReminderSending;

/// <summary>今日の送信予定の一覧の 1 行 (画面に出す文字列)</summary>
/// <param name="item">元の送信予定</param>
/// <remarks>一覧は読み直すたびに作り直すので、値は変わらない。送る先の値は持たない (秘密のため)。</remarks>
public sealed class ReminderPlanItemViewModel(ReminderSendPlanItem item)
{
    /// <summary>元の送信予定 (読み直したときに、変わった行だけ差し替えるための比較にも使う)</summary>
    public ReminderSendPlanItem Source { get; } = item;

    /// <summary>送る時刻 (HH:mm)</summary>
    public string TimeText { get; } = ReminderDates.ToTime(item.Target.Reminder.Time) is { } time ? time.ToString("HH:mm") : "";

    /// <summary>件名</summary>
    public string Title { get; } = item.Target.Reminder.Title;

    /// <summary>送信先を登録した人の表示名</summary>
    public string OwnerName { get; } = item.Target.OwnerName;

    /// <summary>送信先 (種類と登録名)</summary>
    public string ChannelText { get; } = SendDisplay.ChannelText(item.Target.Channel.Kind, item.Target.Channel.Name);

    /// <summary>状態 (「未送信」「送信中」「送信済み」「失敗」「対象外 (過去)」)</summary>
    public string StatusText { get; } = ReminderSendDisplay.PlanStatusText(item);

    /// <summary>右クリックのメニューの文字 (まだ送っていないものは「今すぐ送る」、送ろうとしたものは「再送」)</summary>
    public string ResendText { get; } = item.Status is null ? "今すぐ送る" : "再送";
}
