namespace MmmTool.Notes.Core;

/// <summary>ノートの入力の上限で、DB の列の長さと同じ。UTF-16 の 1 単位で数える</summary>
/// <remarks>本文には上限を決めない。DB の列の最大まで入る。</remarks>
public static class NoteLimits
{
    /// <summary>タイトルの最大文字数</summary>
    public const int TitleMaxLength = 100;

    /// <summary>1 つのノートに入れられる、ほかの実施日の最大の数。Web 版の API が、上限のない一覧を受け取らないための値</summary>
    public const int OtherEventDatesMaxCount = 366;
}
