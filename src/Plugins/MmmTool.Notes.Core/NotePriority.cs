namespace MmmTool.Notes.Core;

/// <summary>ノートの重要度で、5 段階。数が大きいほど重要で、DB にもこの数で保存する</summary>
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

/// <summary>重要度の文字の印。帯の色だけで区別させないため、「高」「最高」に添える</summary>
public static class NotePriorityBadge
{
    /// <summary>印の文字。「高」「最高」以外は、印を付けないので null</summary>
    /// <param name="priority">重要度の数 (1 から 5)</param>
    /// <returns>印の文字か null</returns>
    public static string? Text(int priority) => (NotePriority)priority switch
    {
        NotePriority.Highest => "重要度 最高",
        NotePriority.High => "重要度 高",
        _ => null,
    };
}
