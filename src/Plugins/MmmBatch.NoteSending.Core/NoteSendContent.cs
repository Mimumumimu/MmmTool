using MmmBatch.Sending.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.NoteSending.Core;

/// <summary>ノートの送信の、送る内容と、送信のログでの機能の印</summary>
public static class NoteSendContent
{
    /// <summary>送信のログの機能のキー <see cref="SendLogEntry.Source"/>。ログのファイルに残るので、変えない</summary>
    public const string Source = "Note";

    /// <summary>通知の文で、タイトルの先頭の最大の長さ</summary>
    private const int SummaryMaxLength = 30;

    /// <summary>ノートの作成・更新から、送る内容を作る</summary>
    /// <param name="noteId">ノートの番号。Web 版が、通知を押したときに開く先を決める</param>
    /// <param name="title">ノートのタイトル</param>
    /// <param name="editorName">最後に更新した人の表示名。分からなければ空文字</param>
    /// <param name="isCreated">作成の通知なら true、更新なら false</param>
    /// <returns>
    /// 送る内容。リンクは付けない。どの送信先でも、見出しは更新した人で、本文は「【タイトル】を作成しました」か「【タイトル】を更新しました」。
    /// Discord では、見出しが太字の 1 行目になる。更新した人が分からないときは、ntfy と Discord は見出しを付けず、Web 版通知は「ノート」にする。
    /// </returns>
    public static SendMessage FromChange(int noteId, string title, string editorName, bool isCreated)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(editorName);

        var editor = editorName.Trim();
        var body = NoteNotificationText.Body(title, isCreated);
        var webPush = new WebPushContent(NoteNotificationText.Heading(editor), body, NoteNotificationText.ItemKey(noteId));
        return new SendMessage(editor.Length == 0 ? null : editor, body, null, TitleInContent: true, WebPush: webPush);
    }

    /// <summary>通知の文で、どのノートかを示す言葉を作る。送れなかったときのトレイの通知に使う</summary>
    /// <param name="title">ノートのタイトル</param>
    /// <returns>タイトルの先頭。長いときは省く</returns>
    public static string Summary(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        var trimmed = title.Trim();
        return trimmed.Length > SummaryMaxLength ? trimmed[..SummaryMaxLength] + "…" : trimmed;
    }
}
