namespace MmmTool.Notes.Main;

/// <summary>カード一覧の表示の状態で作り直しても読んでいる位置が変わらないように作り直す前に控える</summary>
/// <param name="ScrollTop">上端からのスクロールの位置 (CSS のピクセル)</param>
/// <param name="OpenIds">開いているカードのノートの番号</param>
/// <param name="KnownIds">
/// 作り直す前に出ていたカードのノートの番号で、これに無いカードは新しく出たもの。
/// 「全文」「タイトルだけ」の既定の開き方にする。
/// </param>
public sealed record NoteViewState(double ScrollTop, IReadOnlySet<int> OpenIds, IReadOnlySet<int> KnownIds);
