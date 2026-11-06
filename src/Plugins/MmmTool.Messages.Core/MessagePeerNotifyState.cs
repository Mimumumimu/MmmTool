namespace MmmTool.Messages.Core;

/// <summary>
/// 1 対 1 の相手に、メッセージの通知が届くか。
/// </summary>
/// <param name="PeerNotifiable">相手に通知が届くとき true。相手が通知先を指定していないときは false。相手を決めないときと、グループは true</param>
/// <remarks>送れるかどうかは決めない。false のときは、送れるが相手に通知が届かないことを、入力欄の近くで知らせる。</remarks>
public sealed record MessagePeerNotifyState(bool PeerNotifiable);
