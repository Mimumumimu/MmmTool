namespace MmmTool.Notes.Core;

/// <summary>本文に付けられる文字色 1 色でパレットの 1 つ</summary>
/// <param name="Index">パレットの中の位置 (表示の CSS のクラス名に使う)</param>
/// <param name="Name">色の名前 (ツールチップに出す)</param>
/// <param name="Hex">
/// ライトモードで読める色で、<c>#RRGGBB</c> の形。
/// 本文にはこの値が <c>color:#RRGGBB</c> の形で入る。
/// </param>
/// <param name="DarkHex">ダークモードで読める、同じ系統の色 (<c>#RRGGBB</c>)。</param>
public sealed record NoteColor(int Index, string Name, string Hex, string DarkHex);
