namespace MmmTool.Notes.Core;

/// <summary>
/// カード一覧のページを作るときの条件。
/// </summary>
/// <param name="Day">表示する日</param>
/// <param name="IsDark">ダークモードか</param>
/// <param name="IsShared">複数の人で共有するか (DB モード。作成者・更新者・「自分だけ」の印を出す)</param>
/// <param name="TitleOnly">「タイトルだけ」か (false は「全文」)</param>
/// <param name="OpenIds">開いた状態にするカードの番号。null なら、「全文」は全部、「タイトルだけ」は全部閉じる</param>
/// <param name="KnownIds">作り直す前に出ていたカードの番号。<paramref name="OpenIds"/> があるとき、これに無いカード (新しく出たもの)は、「全文」なら開き、「タイトルだけ」なら閉じる</param>
public sealed record NotePageOptions(DateOnly Day, bool IsDark, bool IsShared, bool TitleOnly, IReadOnlySet<int>? OpenIds = null, IReadOnlySet<int>? KnownIds = null);
