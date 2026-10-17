namespace MmmTool.Notes.Core;

/// <summary>カードの上の行にある操作</summary>
public enum NoteAction
{
    /// <summary>編集する</summary>
    Edit,

    /// <summary>削除する (論理削除)</summary>
    Delete,

    /// <summary>削除したものを戻す</summary>
    Restore,
}
