namespace MmmTool.Notes.Main;

/// <summary>
/// カード一覧を表示する面 (ViewModel が、画面の型を知らずに使う口)。
/// </summary>
public interface INoteCardsSurface
{
    /// <summary>今の表示の状態を取得する</summary>
    /// <returns>スクロールの位置と、開いているカード。まだ表示できていないときは null</returns>
    Task<NoteViewState?> CaptureStateAsync();

    /// <summary>ページを表示する (表示中なら、作り直す)</summary>
    /// <param name="html">ページの HTML</param>
    /// <param name="scrollTop">表示したあとの、上端からのスクロールの位置 (CSS のピクセル)</param>
    /// <param name="allowedLinks">開いてよいリンク先 (今のページに出したリンクだけ。<see cref="MmmTool.Notes.Core.NoteLinks.CollectHrefs"/>)</param>
    void Show(string html, double scrollTop, IReadOnlySet<string> allowedLinks);
}
