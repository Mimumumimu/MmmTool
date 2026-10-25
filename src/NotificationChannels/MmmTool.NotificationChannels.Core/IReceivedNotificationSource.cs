namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// 受け取った通知を、種類ごとに提供する口。
/// </summary>
/// <remarks>
/// 「通知」の機能は、この口の実装を集めて、一覧を作る。メッセージやリマインダーの名前は持たない。
/// 通知の種類を足すときは、その種類が実装を登録するだけで、「通知」の機能は変えない。
/// 読むのは、今のユーザーが使用者の送信先に届いた分だけで、送信済みのものに限る。
/// </remarks>
public interface IReceivedNotificationSource
{
    /// <summary>種類のキー。通知の識別子の先頭になり、種類の間で重ならない</summary>
    string Key { get; }

    /// <summary>画面に出す、種類の名前</summary>
    string KindName { get; }

    /// <summary>新しい順に、受け取った通知を取得する</summary>
    /// <param name="count">取得する最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>受け取った通知。新しい順</returns>
    Task<IReadOnlyList<ReceivedNotification>> GetLatestAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>番号を指定して、受け取った通知を 1 件取得する</summary>
    /// <param name="id">元の行の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>受け取った通知。無い・自分の送信先に届いたものではない・送信済みではないときは null</returns>
    Task<ReceivedNotification?> GetAsync(int id, CancellationToken cancellationToken = default);
}
