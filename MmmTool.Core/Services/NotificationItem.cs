namespace MmmTool.Core.Services;

/// <summary>
/// 通知ダイアログの本文の 1 項目。
/// </summary>
/// <param name="Text">表示テキスト。</param>
/// <param name="LinkPath">リンク先（URL・ファイル・フォルダのパス）。無ければ通常のテキストとして表示する。</param>
public sealed record NotificationItem(string Text, string? LinkPath = null);
