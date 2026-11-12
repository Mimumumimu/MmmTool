namespace MmmTool.Notes.Core;

/// <summary>ノートのページで表示する日にどのノートを出すかの基準</summary>
public enum NoteBasis
{
    /// <summary>その日に有効なノート</summary>
    Period,

    /// <summary>その日に最後に更新されたノート</summary>
    Updated,
}
