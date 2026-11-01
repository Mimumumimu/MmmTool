namespace MmmBatch.MessageSending.Core;

/// <summary>
/// 送る準備の結果。無効の人の通知を止めた件数と、送る対象になりうる通知の状態。
/// </summary>
/// <param name="SkippedCount">管理者にメッセージを無効にされた人への通知を、送らなかったことにした件数</param>
/// <param name="Targets">送る対象になりうる通知の状態。番号の順で、待つ時間で絞る前のすべて</param>
public sealed record MessageSendBatch(int SkippedCount, IReadOnlyList<MessageSendTarget> Targets);
