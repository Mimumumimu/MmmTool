namespace MmmTool.CliAssist.Main;

/// <summary>入力欄の中に並べる、よく使う文またはグループのチップ 1 つ</summary>
/// <param name="Label">チップに表示する名前</param>
/// <param name="Text">入力欄に入れる文 (グループは null)</param>
/// <param name="Items">グループの中の文 (文は null)</param>
public sealed record QuickMessageItem(string Label, string? Text, IReadOnlyList<QuickMessageItem>? Items = null)
{
    /// <summary>グループか</summary>
    public bool IsGroup => Items is not null;
}
