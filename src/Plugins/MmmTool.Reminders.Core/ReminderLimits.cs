namespace MmmTool.Reminders.Core;

/// <summary>
/// リマインダーの文字数の上限。入力画面の文字数の制限に使う。
/// </summary>
/// <remarks>
/// 文字数は UTF-16 の 1 単位で数える (絵文字などは 2)。<c>TextBox.MaxLength</c> も、DB の <c>nvarchar(n)</c> も同じ数え方なので、
/// 画面で入力できる長さは、DB の列にも入る。DB の列の長さ (<c>docs/specs/database.md</c>・<c>Sql/</c>)は、この値とそろえる。
/// </remarks>
public static class ReminderLimits
{
    /// <summary>件名の最大文字数</summary>
    public const int TitleMaxLength = 200;

    /// <summary>備考の最大文字数</summary>
    public const int NoteMaxLength = 1000;

    /// <summary>リンクの最大文字数</summary>
    public const int LinkMaxLength = 2000;
}
