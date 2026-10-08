using MmmBatch.Sending.Core;

namespace MmmBatch.Sending;

/// <summary>送信の履歴の一覧の 1 行 (画面に出す文字列)</summary>
/// <param name="item">元の送信の状況</param>
/// <remarks>一覧は読み直すたびに作り直すので、値は変わらない。送る先の値は持たない (秘密のため)。</remarks>
public sealed class SendHistoryItemViewModel(SendHistoryItem item)
{
    /// <summary>元の送信の状況</summary>
    public SendHistoryItem Source { get; } = item;

    /// <summary>最後に更新した日時 (「2026/10/08 09:00」の形。ローカル時刻)</summary>
    public string TimeText { get; } = item.Status.UpdatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");

    /// <summary>リマインダーの件名</summary>
    public string Title => Source.Title;

    /// <summary>送信先を登録した人の表示名</summary>
    public string OwnerName => Source.OwnerName;

    /// <summary>送信先 (種類と登録名)</summary>
    public string ChannelText { get; } = SendDisplay.ChannelText(item.ChannelKind, item.ChannelName);

    /// <summary>結果 (「送信済み」「送信中」。失敗は「失敗」と理由)</summary>
    public string ResultText { get; } = SendDisplay.StatusText(item.Status);
}
