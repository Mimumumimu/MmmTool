using MmmSdk.Core.Components.Storage;

namespace MmmBatch.ReminderSending.Core;

/// <summary>送る対象の読み出し口</summary>
/// <remarks>
/// 毎分の仕事は新しく送るもの <see cref="GetDueTargetsAsync"/> と、送り直すもの <see cref="GetRetryTargetsAsync"/> を、それぞれ仕事の残っている行だけ読む。
/// 画面に今日の送信予定を出すときだけ、全件を読む <see cref="GetTargetsAsync"/> を使う。
/// </remarks>
public interface IReminderSendTargetSource
{
    /// <summary>今日発動し送信先を選んでいる削除されていないリマインダーとその送信先を今日・昨日の送信の状況つきで取得する</summary>
    /// <param name="today">
    /// 今日
    /// 昨日はこの前日
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 送る対象と、今日・昨日の送信の状況。
    /// 今日発動するもので、時刻は絞らない。
    /// </returns>
    /// <remarks>
    /// 対象と状況を 1 回の問い合わせで読む。
    /// 対象ごとに状況を引かない。
    /// 画面に今日の送信予定を出すためだけに使い、毎分の仕事では使わない。
    /// </remarks>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task<IReadOnlyList<ReminderSendTargetState>> GetTargetsAsync(DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>今日の分で時刻が範囲の中にあり送信の状況がまだ無い対象を取得する</summary>
    /// <param name="today">今日</param>
    /// <param name="after">
    /// 範囲の始まりの分で、この分より後を読む。
    /// null なら 0:00 から読む。
    /// 起動した直後と日付が変わった直後に、止まっていた間の分へ追いつくため。
    /// </param>
    /// <param name="through">
    /// 範囲の終わりの分で、この分の 59 秒までを読む。
    /// DB の時刻は秒を持てるため。
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 送る対象
    /// 今日の送信の状況がある対象は含めない
    /// <see cref="ReminderSendTargetState.Today"/> は常に null
    /// <see cref="ReminderSendTargetState.Yesterday"/> は読むが使わない
    /// </returns>
    /// <remarks>
    /// 日付と曜日と時刻の絞り込みは DB の条件で行う
    /// 送信設定を今日の発動時刻より後に付けた対象を外す判断は <see cref="ReminderSendService"/> が読んだあとに行う
    /// </remarks>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task<IReadOnlyList<ReminderSendTargetState>> GetDueTargetsAsync(DateOnly today, TimeOnly? after, TimeOnly through, CancellationToken cancellationToken = default);

    /// <summary>送り終えておらず回数の上限に達していない今日・昨日の送信の状況がある対象を取得する</summary>
    /// <param name="today">
    /// 今日
    /// 昨日はこの前日
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 送り直しの候補の対象と、今日・昨日の送信の状況。
    /// 送信設定が今もあるものだけで、削除されたリマインダーと送信先は含めない。
    /// 待ち時間が過ぎたかは見ず、見るのは <see cref="ReminderSendService"/>。
    /// </returns>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task<IReadOnlyList<ReminderSendTargetState>> GetRetryTargetsAsync(DateOnly today, CancellationToken cancellationToken = default);
}
