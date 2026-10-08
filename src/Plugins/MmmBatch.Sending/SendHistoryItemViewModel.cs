using MmmBatch.Sending.Core;

namespace MmmBatch.Sending;

/// <summary>送信の履歴の一覧の 1 行 (画面に出す文字列)</summary>
/// <param name="entry">送信のログの 1 行</param>
/// <remarks>一覧は読み直すたびに作り直すので、値は変わらない。送る先の値は持たない (秘密のため)。</remarks>
public sealed class SendHistoryItemViewModel(SendLogEntry entry)
{
    /// <summary>元のログの行</summary>
    public SendLogEntry Source { get; } = entry;

    /// <summary>送った日時 (「2026/10/08 09:00:05」の形。ローカル時刻)</summary>
    public string TimeText { get; } = entry.At.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");

    /// <summary>リマインダーの件名</summary>
    public string Title => Source.Title;

    /// <summary>送信先を登録した人の表示名</summary>
    public string OwnerName => Source.OwnerName;

    /// <summary>送信先 (種類と登録名)</summary>
    public string ChannelText { get; } = SendDisplay.ChannelText(entry.ChannelKind, entry.ChannelName);

    /// <summary>結果 (「成功」「失敗」。失敗は理由つき)</summary>
    public string ResultText { get; } = SendDisplay.ResultText(entry.Status, entry.Error);
}
