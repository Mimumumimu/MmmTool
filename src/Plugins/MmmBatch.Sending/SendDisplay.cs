using MmmBatch.Sending.Core;
using MmmTool.Reminders.Core;

namespace MmmBatch.Sending;

/// <summary>送信の画面に出す文字列 (今日の送信予定と履歴で、同じ表現にそろえる)</summary>
public static class SendDisplay
{
    /// <summary>送信先を、種類と登録名にして、画面に出す文字列にする</summary>
    /// <param name="kind">送信先の区分。送信先が無ければ null</param>
    /// <param name="name">送信先の登録名</param>
    /// <returns>「登録名 (ntfy)」「登録名 (Discord)」の形 (名前を先に、種類を補足として添える)。送信先が無ければ空文字</returns>
    /// <remarks>送る先の値 (トピック名・Webhook の URL)は秘密なので、出さない。</remarks>
    public static string ChannelText(NotificationChannelKind? kind, string name)
    {
        var kindText = kind switch
        {
            NotificationChannelKind.Ntfy => "ntfy",
            NotificationChannelKind.Discord => "Discord",
            _ => "",
        };
        if (name.Length == 0 && kindText.Length == 0)
        {
            return "";
        }

        return kindText.Length == 0 ? name : $"{name} ({kindText})";
    }

    /// <summary>今日の送信予定の状態を、画面に出す文字列にする</summary>
    /// <param name="item">今日の送信予定</param>
    /// <returns>今日は送らないもの (保存した時点で、送る時刻が過ぎていた)は「対象外 (過去)」。ほかは、送信の状況の文字列 (<see cref="StatusText"/>)</returns>
    public static string PlanStatusText(SendPlanItem item) => item.IsSkipped ? "対象外 (過去)" : StatusText(item.Status);

    /// <summary>送った 1 回の結果を、画面に出す文字列にする</summary>
    /// <param name="status">結果 (送信済み・失敗)</param>
    /// <param name="error">失敗の理由。成功は空文字</param>
    /// <returns>「成功」「失敗」。失敗は、理由があれば「失敗 (理由)」</returns>
    public static string ResultText(SendStatus status, string error) => status switch
    {
        SendStatus.Sent => "成功",
        SendStatus.Failed when error.Length == 0 => "失敗",
        SendStatus.Failed => $"失敗 ({error})",
        _ => status.ToString(),
    };

    /// <summary>送信の状況を、画面に出す文字列にする</summary>
    /// <param name="status">送信の状況。まだ送ろうとしていなければ null</param>
    /// <returns>「未送信」「送信中」「送信済み」「失敗」。失敗は、理由があれば「失敗 (理由)」</returns>
    public static string StatusText(ReminderSendStatus? status) => status switch
    {
        null => "未送信",
        { Status: SendStatus.Sent } => "送信済み",
        { Status: SendStatus.Pending } => "送信中",
        { Status: SendStatus.Failed, LastError.Length: 0 } => "失敗",
        { Status: SendStatus.Failed } => $"失敗 ({status.LastError})",
        _ => status.Status.ToString(),
    };
}
