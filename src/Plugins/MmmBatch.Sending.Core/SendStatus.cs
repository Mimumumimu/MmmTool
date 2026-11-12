namespace MmmBatch.Sending.Core;

/// <summary>送信の状態</summary>
/// <remarks>
/// リマインダーの送信の状況と送信のログの結果に使う
/// DB には数値で入る
/// 列は <c>dbo.ReminderSendStatus.Status</c>
/// 値を変えると保存済みの内容の意味が変わる
/// 送信のログには名前で残る
/// </remarks>
public enum SendStatus
{
    /// <summary>送信中で、送る前に先に作る</summary>
    /// <remarks>ほかの MmmBatch が同じものを送らないため</remarks>
    Pending = 1,

    /// <summary>送信済み</summary>
    /// <remarks>送信先が受け付けた</remarks>
    Sent = 2,

    /// <summary>失敗</summary>
    Failed = 3,
}
