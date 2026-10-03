namespace MmmTool.Features.CliAssist.Main;

/// <summary>
/// 送信履歴の一覧の 1 行。
/// </summary>
/// <param name="Text">本文の全文（復元・ツールチップに使う）。</param>
/// <param name="Preview">1 行に畳んだ本文。</param>
/// <param name="SentAtText">送信日時の表示。</param>
public sealed record SendHistoryItem(string Text, string Preview, string SentAtText);
