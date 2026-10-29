namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// ノートの作成・更新の通知の、見出しと本文の作り方。PC の通知ダイアログと外部への送信で、同じ形にそろえる。
/// </summary>
/// <remarks>
/// 見出しは「誰から」で、最後に更新した人。本文は「【タイトル】を作成しました」か「【タイトル】を更新しました」で、ノートの本文は載せない。
/// 「見出し + from アプリ名」が、1 つの言い方として読めるようにするため、見出しに本文を入れない。
/// </remarks>
public static class NoteNotificationText
{
    /// <summary>ノートの通知の識別子の先頭「note」。通知の識別子は「種類:番号」の形で、番号はノートの番号。送る側と、通知を押したときの行き先で、合わせる</summary>
    public const string ItemKind = "note";

    /// <summary>見出しを作る</summary>
    /// <param name="editor">最後に更新した人の表示名。分からなければ空文字</param>
    /// <returns>更新した人。分からなければ「ノート」</returns>
    public static string Heading(string editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var trimmed = editor.Trim();
        return trimmed.Length == 0 ? "ノート" : trimmed;
    }

    /// <summary>本文を作る</summary>
    /// <param name="title">ノートのタイトル</param>
    /// <param name="isCreated">作成の通知なら true、更新なら false</param>
    /// <returns>「【タイトル】を作成しました」か「【タイトル】を更新しました」</returns>
    public static string Body(string title, bool isCreated)
    {
        ArgumentNullException.ThrowIfNull(title);

        return $"【{title.Trim()}】を{(isCreated ? "作成" : "更新")}しました";
    }

    /// <summary>通知の識別子を作る</summary>
    /// <param name="noteId">ノートの番号</param>
    /// <returns>「note:番号」</returns>
    public static string ItemKey(int noteId) => $"{ItemKind}:{noteId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}
