namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// リマインダーの通知の、見出しと本文の作り方。通知・外部への送信・「受け取った通知」の一覧で、同じ形にそろえる。
/// </summary>
/// <remarks>
/// 見出しは「何から」で、リマインダー。本文は中身で、1 行目が件名、備考があれば次の行。
/// 「見出し + from アプリ名」が、1 つの言い方として読めるようにするため、見出しに件名を入れない。
/// </remarks>
public static class ReminderNotificationText
{
    /// <summary>見出し</summary>
    public const string Heading = "リマインダー";

    /// <summary>本文を作る</summary>
    /// <param name="title">リマインダーの件名</param>
    /// <param name="note">リマインダーの備考。無ければ null か空文字</param>
    /// <returns>1 行目に「【件名】」。備考があれば、次の行に備考</returns>
    /// <remarks>1 行目の題を【 】で囲むのは、題と本文の境目を、見た目で分かるようにするため。メッセージのタイトルと同じ決まり。</remarks>
    public static string Body(string title, string? note)
    {
        ArgumentNullException.ThrowIfNull(title);

        var heading = $"【{title}】";
        var trimmedNote = note?.Trim() ?? "";
        return trimmedNote.Length == 0 ? heading : $"{heading}\n{trimmedNote}";
    }
}
