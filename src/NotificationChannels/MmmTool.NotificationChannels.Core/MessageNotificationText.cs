namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// メッセージの通知の、見出しと本文の作り方。通知と外部への送信で、同じ形にそろえる。
/// </summary>
/// <remarks>
/// 見出しは「誰から」で、送った人。本文はメッセージの本文。
/// 「見出し + from アプリ名」が、1 つの言い方として読めるようにするため、見出しに本文を入れない。
/// 送った人は、システムが知っているので、本文に書かせない。
/// </remarks>
public static class MessageNotificationText
{
    /// <summary>メッセージの通知の識別子の先頭「message」。通知の識別子は「種類:番号」の形で、番号はメッセージの番号。送る側と、通知を押したときの行き先で、合わせる</summary>
    public const string ItemKind = "message";

    /// <summary>見出しを作る</summary>
    /// <param name="sender">送った人の表示名。分からなければ空文字</param>
    /// <returns>送った人。分からなければ「メッセージ」</returns>
    public static string Heading(string sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        var trimmed = sender.Trim();
        return trimmed.Length == 0 ? "メッセージ" : trimmed;
    }
}
