using MmmTool.Notes.Core;

namespace MmmTool.Notes.Main;

/// <summary>カードの操作 (編集・削除・復元)が押されたときの情報</summary>
/// <param name="Action">操作</param>
/// <param name="NoteId">ノートの番号</param>
public sealed record NoteActionEventArgs(NoteAction Action, int NoteId);
