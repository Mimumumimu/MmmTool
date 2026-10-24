using MmmSdk.Core.Utilities;

namespace MmmTool.Notes.Core;

/// <summary>
/// ノートのページで、画面で選んだ状態。ページを作り直しても引き継ぐために、ページの外で持つ。
/// </summary>
/// <remarks>
/// ページは、別のウィンドウに外す・戻す・機能をオフにしてオンに戻すときに作り直される。そのとき ViewModel は、作るときにここから読み、画面で変えるたびにここへ書く。
/// アプリが動いている間だけ持ち、ファイルには保存しない。
/// </remarks>
public sealed class NotesViewState
{
    /// <summary>表示する日 (同じ日の間だけ引き継ぐ)</summary>
    public RememberedDate Day { get; } = new();

    /// <summary>「タイトルだけ」にするか (false は「全文」)</summary>
    public bool IsTitleOnly { get; set; }

    /// <summary>削除したものも表示するか</summary>
    public bool ShowDeleted { get; set; }
}
