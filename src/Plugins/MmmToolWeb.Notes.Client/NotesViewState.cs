namespace MmmToolWeb.Notes.Client;

/// <summary>
/// ノートの画面の表示の条件で、ホームへ行って戻ったときに保つもの。ブラウザの中だけで持ち、アプリを開き直すと無くなる。
/// </summary>
public sealed class NotesViewState
{
    /// <summary>最後に表示した日。まだ開いていないときは null。</summary>
    public DateOnly? Day { get; set; }

    /// <summary>最後に表示した日が、そのときの今日だったか。そうなら、戻ったときは、日が変わっていても、新しい今日で開く。</summary>
    public bool FollowsToday { get; set; }

    /// <summary>その日に更新したノートを出す基準にしていたか。false は、その日に有効なノート。</summary>
    public bool UpdatedBasis { get; set; }

    /// <summary>「タイトルだけ」を入れていたか。</summary>
    public bool TitleOnly { get; set; }

    /// <summary>「削除したものも表示」を入れていたか。</summary>
    public bool IncludeDeleted { get; set; }
}
