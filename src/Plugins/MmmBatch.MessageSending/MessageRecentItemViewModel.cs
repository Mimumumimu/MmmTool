using MmmBatch.MessageSending.Core;
using MmmTool.Messages.Core;

namespace MmmBatch.MessageSending;

/// <summary>最近の依頼の一覧の 1 行</summary>
/// <param name="target">元の送る対象</param>
/// <remarks>
/// 画面に出す文字列。
/// 一覧は読み直すたびに作り直すので、値は変わらない。
/// 送る先の値は持たない。
/// 秘密のため。
/// </remarks>
public sealed class MessageRecentItemViewModel(MessageSendTarget target)
{
    /// <summary>元の送る対象</summary>
    /// <remarks>読み直したときに変わった行だけ差し替えるための比較にも使う</remarks>
    public MessageSendTarget Source { get; } = target;

    /// <summary>通知を頼んだ日時</summary>
    /// <remarks>「2026/10/08 09:00」の形で、ローカル時刻。</remarks>
    public string TimeText { get; } = target.Request.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");

    /// <summary>メッセージを送った人の表示名</summary>
    public string SenderName { get; } = target.SenderName;

    /// <summary>宛先</summary>
    /// <remarks>通知を受ける人の表示名</remarks>
    public string RecipientText { get; } = target.OwnerName;

    /// <summary>本文の 1 行目</summary>
    public string Title { get; } = MessageSendContent.DisplayTitle(target.Request.Body);

    /// <summary>状態</summary>
    /// <remarks>「未送信」「送信中」「送信済み」「失敗」</remarks>
    public string StatusText { get; } = MessageSendDisplay.StatusText(target.Request);

    /// <summary>右クリックのメニューの文字</summary>
    /// <remarks>まだ送っていないものは「今すぐ送る」、送ろうとしたものは「再送」。</remarks>
    public string ResendText { get; } = target.Request is { Status: MessageSendStatus.Pending, Attempts: 0 } ? "今すぐ送る" : "再送";
}
