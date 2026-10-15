namespace MmmTool.Messages.Core;

/// <summary>メッセージの送信依頼 (<see cref="MessageSendRequest"/>)の状態</summary>
/// <remarks>DB には数値で入る (<c>dbo.MessageSendRequest.Status</c>)。値を変えると、保存済みの内容の意味が変わる。</remarks>
public enum MessageSendStatus
{
    /// <summary>送信待ち、または送信中 (MmmTool が足した直後は送信待ち。MmmBatch が送り始めると送信中)</summary>
    Pending = 1,

    /// <summary>送信済み (送信先が受け付けた)</summary>
    Sent = 2,

    /// <summary>失敗</summary>
    Failed = 3,
}
