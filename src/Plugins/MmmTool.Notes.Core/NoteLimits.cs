namespace MmmTool.Notes.Core;

/// <summary>ノートの入力の上限 (DB の列の長さと同じ。UTF-16 の 1 単位で数える)</summary>
/// <remarks>本文には上限を決めない (DB の列の最大まで)。</remarks>
public static class NoteLimits
{
    /// <summary>タイトルの最大文字数</summary>
    public const int TitleMaxLength = 100;
}
