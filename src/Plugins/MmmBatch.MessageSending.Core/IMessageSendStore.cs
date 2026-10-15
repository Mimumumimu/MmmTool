using MmmSdk.Core.Components.Storage;
using MmmTool.Messages.Core;

namespace MmmBatch.MessageSending.Core;

/// <summary>メッセージの送信依頼 (<see cref="MessageSendRequest"/>)の読み出しと、状態の保存先</summary>
/// <remarks>
/// 依頼の行を足すのは MmmTool。ここは、送る対象を読み、送る権利を取り、結果を書く。
/// 送る権利は、読んだ内容と同じときだけ更新できる条件つきの更新で取る (複数の MmmBatch が動いても、二重に送らない)。
/// </remarks>
public interface IMessageSendStore
{
    /// <summary>送る対象になりうる依頼を取得する</summary>
    /// <param name="maxAttempts">送ろうとする回数の上限。失敗でこれに達した依頼は含めない</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信待ち・送信中の依頼と、失敗で上限に達していない依頼 (番号の順)。待つ時間で絞る前のすべて (絞るのは <see cref="MessageSendService"/>)</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyList<MessageSendTarget>> GetSendableAsync(int maxAttempts, CancellationToken cancellationToken = default);

    /// <summary>最近の依頼を、新しい順に取得する</summary>
    /// <param name="count">取得する最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>依頼 (番号の降順)。日付・状態では絞らない (日をまたいだ失敗も、再送できるように)</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyList<MessageSendTarget>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>送る権利を取る (送信中にし、回数を 1 増やす)</summary>
    /// <param name="current">読んだ内容。状態と更新日時が、いまの行と同じときだけ更新できる</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>権利を取れたとき true (別の MmmBatch が先に取っていれば false)</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task<bool> TryClaimAsync(MessageSendRequest current, CancellationToken cancellationToken = default);

    /// <summary>送信済みにする</summary>
    /// <param name="id">依頼の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新の完了を表すタスク</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task MarkSentAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>失敗にする</summary>
    /// <param name="id">依頼の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>更新の完了を表すタスク</returns>
    /// <remarks>失敗の理由は、ここには残さない (送信のログに残す)。</remarks>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task MarkFailedAsync(int id, CancellationToken cancellationToken = default);
}
