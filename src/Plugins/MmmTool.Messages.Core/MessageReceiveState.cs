namespace MmmTool.Messages.Core;

/// <summary>
/// 自分と相手が、メッセージの受け取り先を指定しているか。
/// </summary>
/// <param name="SelfCanReceive">自分が、受け取り先を指定していて、その送信先が削除されておらず、管理者にメッセージを無効にされていないとき true</param>
/// <param name="PeerCanReceive">相手が受け取れるとき true。相手を決めないときと、グループは true</param>
public sealed record MessageReceiveState(bool SelfCanReceive, bool PeerCanReceive);
