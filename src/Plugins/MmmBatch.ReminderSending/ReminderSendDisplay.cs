using MmmBatch.ReminderSending.Core;
using MmmBatch.Sending.Core;

namespace MmmBatch.ReminderSending;

/// <summary>リマインダーの送信の画面に出す文字列 (今日の送信予定の状態)</summary>
public static class ReminderSendDisplay
{
    /// <summary>今日の送信予定の状態を、画面に出す文字列にする</summary>
    /// <param name="item">今日の送信予定</param>
    /// <returns>今日は送らないもの (保存した時点で、送る時刻が過ぎていた)は「対象外 (過去)」。ほかは、送信の状況の文字列 (<see cref="StatusText"/>)</returns>
    public static string PlanStatusText(ReminderSendPlanItem item) => item.IsSkipped ? "対象外 (過去)" : StatusText(item.Status);

    /// <summary>送信の状況を、画面に出す文字列にする</summary>
    /// <param name="status">送信の状況。まだ送ろうとしていなければ null</param>
    /// <returns>「未送信」「送信中」「送信済み」「失敗」。失敗の理由は出さない (「履歴」で見る)</returns>
    public static string StatusText(ReminderSendStatus? status) => status switch
    {
        null => "未送信",
        { Status: SendStatus.Sent } => "送信済み",
        { Status: SendStatus.Pending } => "送信中",
        { Status: SendStatus.Failed } => "失敗",
        _ => status.Status.ToString(),
    };
}
