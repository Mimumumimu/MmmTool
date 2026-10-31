using MmmBatch.Sending.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.MessageSending.Core;

/// <summary>メッセージの送信の、送る内容と、本文の 1 行目の取り方、送信のログでの機能の印</summary>
public static class MessageSendContent
{
    /// <summary>送信のログの機能のキー <see cref="SendLogEntry.Source"/>。ログのファイルに残るので、変えない</summary>
    public const string Source = "Message";

    /// <summary>通知の文で、本文の先頭の最大の長さ</summary>
    private const int SummaryMaxLength = 30;

    /// <summary>通知の識別子の先頭。Web 版の通知を押したときの行き先の登録と合わせる</summary>
    public const string ItemKind = MessageNotificationText.ItemKind;

    /// <summary>メッセージから、送る内容を作る</summary>
    /// <param name="body">本文</param>
    /// <param name="messageId">メッセージの番号 <c>Message.Id</c>。Web 版が、通知を押したときに開く会話を探す</param>
    /// <param name="senderName">送った人の表示名。分からなければ空文字</param>
    /// <param name="groupName">グループの会話の名前。1 対 1 は空文字</param>
    /// <returns>
    /// 送る内容。リンクは付けない。どの送信先でも、見出しは送った人で、本文はメッセージの本文だけ。
    /// グループの会話の見出しは、「グループ名 / 送った人」にする。
    /// Discord では、見出しが太字の 1 行目になる。送った人が分からないときは、ntfy と Discord は見出しを付けず、Web 版通知は「メッセージ」にする。
    /// </returns>
    public static SendMessage FromMessage(string body, int messageId, string senderName, string groupName)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(senderName);
        ArgumentNullException.ThrowIfNull(groupName);

        var sender = senderName.Trim();
        var group = groupName.Trim();
        var heading = group.Length == 0 ? sender : sender.Length == 0 ? group : $"{group} / {sender}";
        var webPush = new WebPushContent(MessageNotificationText.Heading(heading), body, $"{ItemKind}:{messageId}");
        return new SendMessage(heading.Length == 0 ? null : heading, body, null, TitleInContent: true, WebPush: webPush);
    }

    /// <summary>画面の「本文」列と、送信のログに出す、本文の 1 行目を取る</summary>
    /// <param name="body">本文</param>
    /// <returns>最初の改行より前の文字。前後の空白は除く</returns>
    public static string DisplayTitle(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
    }

    /// <summary>通知の文で、どのメッセージかを示す言葉を作る。送れなかったときのトレイの通知に使う</summary>
    /// <param name="body">本文</param>
    /// <returns>本文の 1 行目の先頭。長いときは省く</returns>
    public static string Summary(string body)
    {
        var firstLine = DisplayTitle(body);
        return firstLine.Length > SummaryMaxLength ? firstLine[..SummaryMaxLength] + "…" : firstLine;
    }
}
