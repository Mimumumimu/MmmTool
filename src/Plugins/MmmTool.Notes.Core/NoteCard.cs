namespace MmmTool.Notes.Core;

/// <summary>
/// カードに出す 1 件分 (ノートと、画面に出す名前・印)。
/// </summary>
/// <param name="Note">ノート</param>
/// <param name="AuthorName">作成者の名前 (ローカルモードは空文字)</param>
/// <param name="UpdatedByName">更新者の名前 (ローカルモードは空文字)</param>
/// <param name="IsUpdatedToday">更新日が、実際の今日か</param>
/// <param name="IsEventDay">表示する日が、実施日か (カードを目立たせる)</param>
/// <param name="IsEventToday">表示する日が、実際の今日でもあるか。帯の文言を決める</param>
public sealed record NoteCard(Note Note, string AuthorName, string UpdatedByName, bool IsUpdatedToday, bool IsEventDay = false, bool IsEventToday = false)
{
    /// <summary>更新者が、作成者と別の人か (別の人のときだけ、更新者を出す)</summary>
    public bool IsUpdatedByOther => Note.UpdatedByUserId != Note.AuthorUserId;
}
