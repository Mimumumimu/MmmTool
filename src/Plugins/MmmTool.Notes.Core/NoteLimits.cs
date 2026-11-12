namespace MmmTool.Notes.Core;

/// <summary>ノートの入力の上限で、DB の列の長さと同じ</summary>
/// <remarks>
/// UTF-16 の 1 単位で数える
/// 本文には上限を決めない
/// DB の列の最大まで入る
/// </remarks>
public static class NoteLimits
{
    /// <summary>タイトルの最大文字数</summary>
    public const int TitleMaxLength = 100;

    /// <summary>1 つのノートに入れられるほかの実施日の最大の数</summary>
    /// <remarks>Web 版の API が上限のない一覧を受け取らないための値</remarks>
    public const int OtherEventDatesMaxCount = 366;
}
