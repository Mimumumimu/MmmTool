using MmmBatch.MessageSending.Core;
using MmmTool.Messages.Core;

namespace MmmBatch.MessageSending;

/// <summary>最近の依頼の一覧の 1 行。画面に出す文字列</summary>
/// <param name="target">元の送る対象</param>
/// <remarks>一覧は読み直すたびに作り直すので、値は変わらない。送る先の値は持たない。秘密のため。</remarks>
public sealed class MessageRecentItemViewModel(MessageSendTarget target)
{
    /// <summary>元の送る対象。読み直したときに、変わった行だけ差し替えるための比較にも使う</summary>
    public MessageSendTarget Source { get; } = target;

    /// <summary>通知を頼んだ日時。「2026/10/08 09:00」の形で、ローカル時刻</summary>
    public string TimeText { get; } = target.Request.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");

    /// <summary>メッセージを送った人の表示名</summary>
    public string SenderName { get; } = target.SenderName;

    /// <summary>宛先。通知を受ける人の表示名</summary>
    public string RecipientText { get; } = target.OwnerName;

    /// <summary>本文の 1 行目</summary>
    public string Title { get; } = MessageSendContent.DisplayTitle(target.Request.Body);

    /// <summary>状態。「未送信」「送信中」「送信済み」「失敗」</summary>
    public string StatusText { get; } = MessageSendDisplay.StatusText(target.Request);

    /// <summary>右クリックのメニューの文字。まだ送っていないものは「今すぐ送る」、送ろうとしたものは「再送」</summary>
    public string ResendText { get; } = target.Request is { Status: MessageSendStatus.Pending, Attempts: 0 } ? "今すぐ送る" : "再送";
}
