namespace MmmTool.CliAssist.Core;

/// <summary>送信履歴の 1 件</summary>
/// <param name="Text">送信した本文の全文。</param>
/// <param name="SentAt">送信日時。</param>
public sealed record SendHistoryEntry(string Text, DateTimeOffset SentAt);
