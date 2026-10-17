namespace MmmTool.Notes.Core;

/// <summary>
/// カードに出す 1 件分 (ノートと、画面に出す名前・印)。
/// </summary>
/// <param name="Note">ノート</param>
/// <param name="AuthorName">作成者の名前 (ローカルモードは空文字)</param>
/// <param name="UpdatedByName">更新者の名前 (ローカルモードは空文字)</param>
/// <param name="IsUpdatedToday">更新日が、実際の今日か</param>
public sealed record NoteCard(Note Note, string AuthorName, string UpdatedByName, bool IsUpdatedToday)
{
    /// <summary>更新者が、作成者と別の人か (別の人のときだけ、更新者を出す)</summary>
    public bool IsUpdatedByOther => Note.UpdatedByUserId != Note.AuthorUserId;
}
