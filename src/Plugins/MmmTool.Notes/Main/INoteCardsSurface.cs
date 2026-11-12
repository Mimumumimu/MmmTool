namespace MmmTool.Notes.Main;

/// <summary>カード一覧を表示する面でViewModel が画面の型を知らずに使う口</summary>
public interface INoteCardsSurface
{
    /// <summary>今の表示の状態を取得する</summary>
    /// <returns>
    /// スクロールの位置と、開いているカード。
    /// まだ表示できていないときは null。
    /// </returns>
    Task<NoteViewState?> CaptureStateAsync();

    /// <summary>ページを表示する</summary>
    /// <param name="html">ページの HTML</param>
    /// <param name="scrollTop">表示したあとの、上端からのスクロールの位置 (CSS のピクセル)。</param>
    /// <param name="allowedLinks">
    /// 開いてよいリンク先で、今のページに出したリンクだけ。
    /// 集めるのは <see cref="MmmTool.Notes.Core.NoteLinks.CollectHrefs"/>。
    /// </param>
    /// <remarks>表示中なら、作り直す。</remarks>
    void Show(string html, double scrollTop, IReadOnlySet<string> allowedLinks);
}
