using MmmSdk.Core.Components.Storage;

namespace MmmBatch.MessageSending.Core;

/// <summary>メッセージの通知の状態 MessageDeliveryItem の読み出しと状態の保存先</summary>
/// <remarks>
/// 通知の状態の行を足すのは MmmTool と Web 版。
/// ここは送る対象を読み、送る権利を取り、結果を書く。
/// 送る先は送るたびに通知を受ける人の通知先から読む。
/// 送る権利は読んだ内容と同じときだけ更新できる条件つきの更新で取る。
/// 複数の MmmBatch が動いても二重に送らない。
/// 状態を変えるときは同じトランザクションで、そのメッセージの更新日時も更新する。
/// 端末の差分の取得に状態の変わりが届くため。
/// </remarks>
public interface IMessageSendStore
{
    /// <summary>無効の人の通知を止めてから、送る対象になりうる通知の状態を取得する</summary>
    /// <param name="maxAttempts">
    /// 送ろうとする回数の上限
    /// 失敗でこれに達したものは含めない
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 止めた件数と、送る対象。
    /// 対象はいま送ってよいもので、番号の順。
    /// 足したばかりのもの、失敗して待ち時間が過ぎたもの、送信中のまま止まって待ち時間が過ぎたもの。
    /// 回数が上限に達したものは含まない。
    /// 待ち時間は送り直しの決まり <c>SendRetryPolicy</c> を、DB の時計で当てる。
    /// 待ち時間が残るものは含まない。
    /// </returns>
    /// <remarks>
    /// 通知を受ける人が管理者にメッセージを無効にされている通知の状態を、送らなかったことにする。
    /// 対象は送信待ち・送信中・失敗のもので、送信済みは変えない。
    /// 有効に戻っても古い通知は送り直さない。
    /// メッセージ本体は消さない。
    /// 止める更新と読み出しを 1 回の往復で行う。
    /// 止めたものは読み出しに含まれない。
    /// </remarks>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task<MessageSendBatch> SkipDisabledAndGetSendableAsync(int maxAttempts, CancellationToken cancellationToken = default);

    /// <summary>最近の通知の状態を新しい順に取得する</summary>
    /// <param name="count">取得する最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 通知の状態。
    /// 番号の降順。
    /// 日付・状態では絞らない。
    /// 日をまたいだ失敗も、再送できるように。
    /// </returns>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task<IReadOnlyList<MessageSendTarget>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>送る権利を取る</summary>
    /// <param name="current">
    /// 読んだ内容
    /// 状態と更新日時がいまの行と同じときだけ更新できる
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 権利を取れたとき true
    /// 別の MmmBatch が先に取っていれば false
    /// </returns>
    /// <remarks>送信中にし、回数を 1 増やす。</remarks>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task<bool> TryClaimAsync(MessageDeliveryItem current, CancellationToken cancellationToken = default);

    /// <summary>複数の通知の送る権利をまとめて取る</summary>
    /// <param name="items">
    /// 読んだ内容。
    /// それぞれ、状態と更新日時がいまの行と同じときだけ更新できる。
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 権利を取れた通知の状態の番号
    /// 別の MmmBatch が先に取っていたものは含まない
    /// </returns>
    /// <remarks>
    /// 取れたものを送信中にし、回数を 1 増やす。
    /// 行ごとに <see cref="TryClaimAsync"/> を呼ぶ代わりに使う。
    /// 条件は 1 件のときと同じで、1 回の往復で更新する。
    /// </remarks>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task<IReadOnlySet<int>> TryClaimManyAsync(IReadOnlyList<MessageDeliveryItem> items, CancellationToken cancellationToken = default);

    /// <summary>送信済みにする</summary>
    /// <param name="id">通知の状態の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新の完了を表すタスク</returns>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task MarkSentAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>失敗にする</summary>
    /// <param name="id">通知の状態の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新の完了を表すタスク</returns>
    /// <remarks>
    /// 失敗の理由はここには残さない
    /// 送信のログに残す
    /// </remarks>
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・表が無い・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task MarkFailedAsync(int id, CancellationToken cancellationToken = default);
}
