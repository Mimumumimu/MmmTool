using MmmSdk.Core.Utilities;

namespace MmmTool.Notes.Core;

/// <summary>ノートのページで、画面で選んだ状態</summary>
/// <remarks>
/// ページを作り直しても引き継ぐためにページの外で持つ。
/// ページは別のウィンドウに外す・戻す・機能をオフにしてオンに戻すときに作り直される。
/// そのとき ViewModel は作るときにここから読み、画面で変えるたびにここへ書く。
/// アプリが動いている間だけ持ち、ファイルには保存しない。
/// </remarks>
public sealed class NotesViewState
{
    /// <summary>表示する日</summary>
    /// <remarks>同じ日の間だけ引き継ぐ</remarks>
    public RememberedDate Day { get; } = new();

    /// <summary>その日にどのノートを出すかの基準</summary>
    public NoteBasis Basis { get; set; } = NoteBasis.Period;

    /// <summary>「タイトルだけ」にするか</summary>
    public bool IsTitleOnly { get; set; }

    /// <summary>削除したものも表示するか</summary>
    public bool ShowDeleted { get; set; }
}
