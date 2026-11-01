namespace MmmBatch.ReminderSending.Core;

/// <summary>
/// 送る対象と、その今日・昨日の送信の状況。
/// </summary>
/// <param name="Target">送る対象</param>
/// <param name="Today">今日の送信の状況。まだ送ろうとしていないとき null</param>
/// <param name="Yesterday">昨日の送信の状況。昨日は送ろうとしていないとき null</param>
public sealed record ReminderSendTargetState(ReminderSendTarget Target, ReminderSendStatus? Today, ReminderSendStatus? Yesterday);
