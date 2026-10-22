namespace MmmTool.Notes.Core;

/// <summary>
/// ノートのカードの上に出す、他の機能の内容 1 枠で、見出しと文を持つ。
/// </summary>
/// <param name="Title">見出し</param>
/// <param name="Text">文 (改行と字下げは、そのまま出す)</param>
public sealed record DayContentFrame(string Title, string Text);
