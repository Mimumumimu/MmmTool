using MmmBatch.Sending.Core;
using MmmTool.Reminders.Core;

namespace MmmBatch.Sending;

/// <summary>今日の送信予定の一覧の 1 行 (画面に出す文字列)</summary>
/// <param name="item">元の送信予定</param>
/// <remarks>一覧は読み直すたびに作り直すので、値は変わらない。送る先の値は持たない (秘密のため)。</remarks>
public sealed class SendPlanItemViewModel(SendPlanItem item)
{
    /// <summary>元の送信予定</summary>
    public SendPlanItem Source { get; } = item;

    /// <summary>送る時刻 (HH:mm)</summary>
    public string TimeText { get; } = ReminderDates.ToTime(item.Target.Reminder.Time) is { } time ? time.ToString("HH:mm") : "";

    /// <summary>リマインダーの件名</summary>
    public string Title => Source.Target.Reminder.Title;

    /// <summary>送信先を登録した人の表示名</summary>
    public string OwnerName => Source.Target.OwnerName;

    /// <summary>送信先 (種類と登録名)</summary>
    public string ChannelText { get; } = SendDisplay.ChannelText(item.Target.Channel.Kind, item.Target.Channel.Name);

    /// <summary>状態 (「未送信」「送信中」「送信済み」「失敗」「対象外 (過去)」。失敗は理由つき)</summary>
    public string StatusText { get; } = SendDisplay.PlanStatusText(item);

    /// <summary>右クリックのメニューの文字 (まだ送っていないものは「今すぐ送る」、送ろうとしたものは「再送」)</summary>
    public string ResendText { get; } = item.Status is null ? "今すぐ送る" : "再送";
}
