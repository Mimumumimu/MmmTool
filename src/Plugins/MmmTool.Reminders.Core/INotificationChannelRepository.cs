using MmmSdk.Core.Components.Storage;

namespace MmmTool.Reminders.Core;

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

    /// <summary>番号から、送信先を 1 件取得する (ほかの人が登録したものも含む)</summary>
    /// <param name="id">送信先の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送信先。無ければ null</returns>
    /// <remarks>ほかの人が登録した送信先を、見えるリマインダーの編集で、選択肢に足すために使う。</remarks>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった (メッセージは画面に出せる)。</exception>
    Task<NotificationChannel?> FindAsync(int id, CancellationToken cancellationToken = default);

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
