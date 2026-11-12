namespace MmmBatch.ReminderSending.Core;

/// <summary>今日の送信予定の 1 行で送る対象と今日の送信の状況</summary>
/// <param name="Target">送る対象</param>
/// <param name="Status">
/// 今日の送信の状況。
/// まだ送ろうとしていなければ null (未送信、または今日は送らない)。
/// </param>
/// <param name="IsSkipped">今日は送らないか (まだ送ろうとしておらず、保存した時点で、送る時刻が過ぎていた)。</param>
/// <remarks>
/// 今日は送らないもの (状態は「対象外 (過去)」)も今日の分として一覧に出す。
/// なぜ送られないか、見えるようにするため。
/// 送る処理 (毎分の実行)はこれを送らない。
/// </remarks>
public sealed record ReminderSendPlanItem(ReminderSendTarget Target, ReminderSendStatus? Status, bool IsSkipped);
