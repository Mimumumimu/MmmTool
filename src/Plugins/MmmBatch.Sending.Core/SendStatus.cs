namespace MmmBatch.Sending.Core;

/// <summary>送信の状態 (リマインダーの送信の状況と、送信のログの結果に使う)</summary>
/// <remarks>DB には数値で入る (<c>dbo.ReminderSendStatus.Status</c>)。値を変えると、保存済みの内容の意味が変わる。送信のログには名前で残る。</remarks>
public enum SendStatus
{
    /// <summary>送信中 (送る前に、先に作る。ほかの MmmBatch が同じものを送らないため)</summary>
    Pending = 1,

    /// <summary>送信済み (送信先が受け付けた)</summary>
    Sent = 2,

    /// <summary>失敗</summary>
    Failed = 3,
}
