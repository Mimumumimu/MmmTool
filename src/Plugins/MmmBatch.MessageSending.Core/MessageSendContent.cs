using MmmBatch.Sending.Core;

namespace MmmBatch.MessageSending.Core;

/// <summary>メッセージの送信の、送る内容と、件名の作り方、送信のログでの機能の印</summary>
public static class MessageSendContent
{
    /// <summary>送信のログの機能のキー (<see cref="SendLogEntry.Source"/>)。ログのファイルに残るので、変えない</summary>
    public const string Source = "Message";

    /// <summary>通知の文で、件名の代わりに出す本文の先頭の最大の長さ</summary>
    private const int SummaryMaxLength = 30;

    /// <summary>メッセージ (MmmTool で依頼されたもの)から、送る内容を作る</summary>
    /// <param name="title">タイトル。空なら、タイトルなし</param>
    /// <param name="body">本文</param>
    /// <returns>送る内容。リンクは付けない。Discord では、タイトルがあれば太字の 1 行目になる</returns>
    public static SendMessage FromMessage(string title, string body)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);

        return new SendMessage(string.IsNullOrWhiteSpace(title) ? null : title, body, null, TitleInContent: true);
    }

    /// <summary>画面の「件名」列と、送信のログに出す、依頼の件名を作る</summary>
    /// <param name="title">タイトル。空ならタイトルなし</param>
    /// <returns>前後の空白を除いたタイトル。無ければ空文字 (本文で埋めない。件名が付いていたかを、画面から見分けられるようにするため)</returns>
    public static string DisplayTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        return title.Trim();
    }

    /// <summary>通知の文 (送れなかったときのトレイの通知)で、どの依頼かを示す言葉を作る</summary>
    /// <param name="title">タイトル。空ならタイトルなし</param>
    /// <param name="body">本文</param>
    /// <returns>タイトルがあればタイトル。無ければ、本文の 1 行目の先頭 (長いときは省く)。通知の文だけに使い、件名の列・ログには使わない</returns>
    public static string Summary(string title, string body)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);

        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var firstLine = body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        return firstLine.Length > SummaryMaxLength ? firstLine[..SummaryMaxLength] + "…" : firstLine;
    }
}
