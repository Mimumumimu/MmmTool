namespace MmmTool.Notes.Core;

/// <summary>ノートの重要度 (5 段階)。数が大きいほど重要で、DB にもこの数で保存する</summary>
public enum NotePriority : byte
{
    /// <summary>最低</summary>
    Lowest = 1,

    /// <summary>低</summary>
    Low = 2,

    /// <summary>中 (新規の初期値)</summary>
    Normal = 3,

    /// <summary>高</summary>
    High = 4,

    /// <summary>最高</summary>
    Highest = 5,
}
