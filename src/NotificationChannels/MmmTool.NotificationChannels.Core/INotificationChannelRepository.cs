using MmmSdk.Core.Components.Storage;

namespace MmmTool.NotificationChannels.Core;

/// <summary>送信先 (<see cref="NotificationChannel"/>)の保存先</summary>
/// <remarks>
/// DB モードだけで使う。ローカルモードでは、<see cref="IsAvailable"/> が false の実装 (<see cref="UnavailableNotificationChannelRepository"/>)が入り、画面は、送信先の入口を出さない。
/// 外部へ送る処理は、MmmTool には無い。ここは、送信先を保存して選ぶためだけにある。
/// </remarks>
public interface INotificationChannelRepository
{
    /// <summary>使える保存先か</summary>
    /// <remarks>DB モードは true、ローカルモードは false。false のとき、ほかの操作は <see cref="NotSupportedException"/>。</remarks>
    bool IsAvailable { get; }

    /// <summary>今のユーザーの番号 (<c>AppUser.Id</c>)。まだ特定していない・ローカルモードは 0</summary>
    int CurrentUserId { get; }

    /// <summary>自分が登録した送信先の一覧を取得する</summary>
    /// <param name="includeDeleted">論理削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信先の一覧 (番号の順)</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyList<NotificationChannel>> GetChannelsAsync(bool includeDeleted, CancellationToken cancellationToken = default);

    /// <summary>自分が登録した、ある種類の送信先の要約を取得する</summary>
    /// <param name="kind">種類</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>論理削除されていない送信先の番号と登録名で、番号の順。送る先の値は読まない</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyList<NotificationChannelSummary>> GetSummariesAsync(NotificationChannelKind kind, CancellationToken cancellationToken = default);

    /// <summary>自分が登録した Web 版通知の送信先を、端末の宛先 (配達係の URL)から探す</summary>
    /// <param name="endpoint">端末の宛先の URL</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>見つけた行。複数あるときは、削除されていない行を先にして、番号の大きい方。無ければ null。送る先の値は読まない</returns>
    /// <remarks>端末が登録の番号を覚えていなくても、どの行かを知るために使う。比べるのは宛先の URL だけで、鍵が替わっていても見つかる。</remarks>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<NotificationChannelMatch?> FindWebPushAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>送信先を追加する</summary>
    /// <param name="channel">追加する送信先 (<see cref="NotificationChannel.Id"/> は無視する)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>追加した内容 (保存先が決めた番号つき)</returns>
    /// <exception cref="ArgumentException">登録名・値が空か長すぎる。</exception>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task<NotificationChannel> AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default);

    /// <summary>自分が登録した送信先を、同じ番号の内容で置き換える</summary>
    /// <param name="channel">置き換える内容 (<see cref="NotificationChannel.Id"/> で対象を決める。区分は変えない)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があって置き換えたとき true (無い・自分が登録したものでないとき false)</returns>
    /// <exception cref="ArgumentException">登録名・値が空か長すぎる。</exception>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task<bool> UpdateAsync(NotificationChannel channel, CancellationToken cancellationToken = default);

    /// <summary>自分が登録した送信先の削除フラグ (論理削除)を変える</summary>
    /// <param name="id">送信先の番号</param>
    /// <param name="isDeleted">削除済みにするか (false なら解除)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があれば true (無い・自分が登録したものでないとき false)</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・保存できなかった (メッセージは画面に出せる)。</exception>
    Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default);
}
