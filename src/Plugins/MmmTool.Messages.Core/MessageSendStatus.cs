namespace MmmTool.Messages.Core;

/// <summary>メッセージの通知の状態 (<c>dbo.MessageDelivery.Status</c>)</summary>
/// <remarks>DB には数値で入る。値を変えると、保存済みの内容の意味が変わる。通知が届いたかを表すだけで、メッセージが読めるかには関係しない。</remarks>
public enum MessageSendStatus
{
    /// <summary>送信待ち、または送信中。書いた直後は送信待ちで、MmmBatch が送り始めると送信中</summary>
    Pending = 1,

    /// <summary>送信済み。送信先が受け付けた</summary>
    Sent = 2,

    /// <summary>失敗</summary>
    Failed = 3,
}
