namespace MmmToolWeb.Notes.Client;

/// <summary>ノートの画面の表示の条件で、ホームへ行って戻ったときに保つもの</summary>
/// <remarks>ブラウザの中だけで持ち、アプリを開き直すと無くなる。</remarks>
public sealed class NotesViewState
{
    /// <summary>最後に表示した日</summary>
    /// <remarks>まだ開いていないときは null</remarks>
    public DateOnly? Day { get; set; }

    /// <summary>最後に表示した日がそのときの今日だったか</summary>
    /// <remarks>そうなら、戻ったときは日が変わっていても新しい今日で開く。</remarks>
    public bool FollowsToday { get; set; }

    /// <summary>その日に更新したノートを出す基準にしていたか</summary>
    /// <remarks>false はその日に有効なノート</remarks>
    public bool UpdatedBasis { get; set; }

    /// <summary>「タイトルだけ」を入れていたか</summary>
    public bool TitleOnly { get; set; }

    /// <summary>「削除したものも表示」を入れていたか</summary>
    public bool IncludeDeleted { get; set; }
}
