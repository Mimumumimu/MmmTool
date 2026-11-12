using MmmBatch.Sending.Core;

namespace MmmBatch.Sending;

/// <summary>送信の履歴の一覧の 1 行</summary>
/// <param name="entry">送信の記録の 1 行</param>
/// <remarks>
/// 一覧は読み直すたびに作り直すので、値は変わらない。
/// 送る先の値は持たない。
/// 秘密のため。
/// 機能ごとの画面が使う列だけを出す。
/// </remarks>
public sealed class SendHistoryItemViewModel(SendLogEntry entry)
{
    /// <summary>元の記録の行</summary>
    public SendLogEntry Source { get; } = entry;

    /// <summary>送った日時で、「2026/10/08 09:00:05」の形</summary>
    /// <remarks>ローカル時刻</remarks>
    public string TimeText { get; } = entry.At.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");

    /// <summary>リマインダーの件名</summary>
    /// <remarks>メッセージはタイトル</remarks>
    public string Title => Source.Title;

    /// <summary>送信先を登録した人の表示名</summary>
    public string OwnerName => Source.OwnerName;

    /// <summary>メッセージを依頼した人の表示名</summary>
    /// <remarks>リマインダーは空文字</remarks>
    public string SenderName => Source.SenderName;

    /// <summary>送信先</summary>
    public string ChannelText { get; } = SendDisplay.ChannelText(entry.ChannelKind, entry.ChannelName);

    /// <summary>宛先</summary>
    public string RecipientText { get; } = SendDisplay.RecipientText(entry.OwnerName, entry.ChannelKind, entry.ChannelName);

    /// <summary>結果で、「成功」「失敗」</summary>
    /// <remarks>失敗は理由つき</remarks>
    public string ResultText { get; } = SendDisplay.ResultText(entry.Status, entry.Error);
}
