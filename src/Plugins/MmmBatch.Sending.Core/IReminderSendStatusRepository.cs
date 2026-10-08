using MmmSdk.Core.Components.Storage;

namespace MmmBatch.Sending.Core;

/// <summary>リマインダーの送信の状況 (<see cref="ReminderSendStatus"/>)の保存先</summary>
/// <remarks>
/// 送る前に、先に行を作り、一意制約 (リマインダー × 送信先 × 日)で、作れた 1 つだけが送る (二重送信の防止)。
/// 送り直しの権利は、読んだ内容と同じときだけ更新できる条件つきの更新で取る (複数の MmmBatch が動いても、二重に送らない)。
/// </remarks>
public interface IReminderSendStatusRepository
{
    /// <summary>ある日の送信の状況を取得する</summary>
    /// <param name="date">日付</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>その日の送信の状況</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyList<ReminderSendStatus>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>送信中 (<see cref="SendStatus.Pending"/>・1 回目)の行を作る</summary>
    /// <param name="reminderId">送るリマインダーの番号</param>
    /// <param name="channelId">送る先の番号</param>
    /// <param name="date">どの日の分か</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>作った行。同じ「リマインダー × 送信先 × 日」の行がすでにあれば (ほかが先に作った)null</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task<ReminderSendStatus?> TryCreateAsync(int reminderId, int channelId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>送り直しの権利を取る (送信中に戻し、回数を 1 増やす)</summary>
    /// <param name="current">読んだ内容。状態と更新日時が、いまの行と同じときだけ更新できる</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>権利を取れたとき true (別の MmmBatch が先に取っていれば false)</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task<bool> TryClaimRetryAsync(ReminderSendStatus current, CancellationToken cancellationToken = default);

    /// <summary>送信済みにする</summary>
    /// <param name="id">行の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新の完了を表すタスク</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task MarkSentAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>失敗にする</summary>
    /// <param name="id">行の番号</param>
    /// <param name="error">失敗の理由 (送る先の値を含まない)。長すぎるときは切る</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新の完了を表すタスク</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task MarkFailedAsync(int id, string error, CancellationToken cancellationToken = default);

    /// <summary>新しい順に、送信の状況の一覧を取得する</summary>
    /// <param name="count">取得する最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信の状況 (更新日時の新しい順)</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyList<SendHistoryItem>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
