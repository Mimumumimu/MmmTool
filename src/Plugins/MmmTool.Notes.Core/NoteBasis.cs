namespace MmmTool.Notes.Core;

/// <summary>
/// ノートのページで、表示する日にどのノートを出すかの基準。
/// </summary>
public enum NoteBasis
{
    /// <summary>その日に有効なノート (開始日 ≤ その日 ≤ 終了日)</summary>
    Period,

    /// <summary>その日に最後に更新されたノート (更新日時がその日の 0 時から翌日の 0 時の間)</summary>
    Updated,
}
