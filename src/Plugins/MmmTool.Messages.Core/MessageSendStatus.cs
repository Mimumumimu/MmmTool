namespace MmmTool.Messages.Core;

/// <summary>メッセージの通知の状態</summary>
/// <remarks>
/// DB には数値で入る。
/// 値を変えると保存済みの内容の意味が変わる。
/// 通知が届いたかを表すだけで、メッセージが読めるかには関係しない。
/// </remarks>
public enum MessageSendStatus
{
    /// <summary>送信待ち、または送信中</summary>
    /// <remarks>書いた直後は送信待ちで、MmmBatch が送り始めると送信中。</remarks>
    Pending = 1,

    /// <summary>送信済み</summary>
    /// <remarks>送信先が受け付けた</remarks>
    Sent = 2,

    /// <summary>失敗</summary>
    Failed = 3,

    /// <summary>送らなかった</summary>
    /// <remarks>
    /// 通知を受ける人が管理者にメッセージを無効にされたため、MmmBatch が止めた。
    /// 有効に戻っても送り直さない。
    /// 送った人の画面では送信済みと同じに見える。
    /// 通知先を指定していない人宛てはメッセージを書くときから、この状態で書く。
    /// 画面が自分宛の新着をこの行で見つけるため。
    /// </remarks>
    Skipped = 4,
}
