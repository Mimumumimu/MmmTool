namespace MmmBatch.Sending.Core;

/// <summary>リマインダーの送信の状況 (<see cref="ReminderSendStatus"/>)の状態</summary>
/// <remarks>DB には数値で入る (<c>dbo.ReminderSendStatus.Status</c>)。値を変えると、保存済みの内容の意味が変わる。</remarks>
public enum SendStatus
{
    /// <summary>送信中 (送る前に、先に作る。ほかの MmmBatch が同じものを送らないため)</summary>
    Pending = 1,

    /// <summary>送信済み (送信先が受け付けた)</summary>
    Sent = 2,

    /// <summary>失敗</summary>
    Failed = 3,
}
