namespace MmmTool.Notes.Core;

/// <summary>
/// ノートと、書いた人・最後に更新した人の表示名。
/// </summary>
/// <param name="Note">ノート</param>
/// <param name="AuthorName">書いた人の表示名。人が見つからないときと、共有しないとき (ローカルモード)は空文字</param>
/// <param name="UpdatedByName">最後に更新した人の表示名。人が見つからないときと、共有しないとき (ローカルモード)は空文字</param>
public sealed record NoteWithNames(Note Note, string AuthorName, string UpdatedByName);
