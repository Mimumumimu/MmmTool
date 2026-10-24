namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// 通知を押したときに開く画面を、種類ごとに答える口。Web 版が使う。
/// </summary>
/// <remarks>
/// 通知は「種類:番号」の識別子を持つ。「通知」の機能は、識別子の種類の実装に、開く画面を聞く。
/// メッセージや、リマインダーなどの名前は持たない。登録が無い種類は、通知の一覧を開く。
/// 受け取った一覧の提供元 <see cref="IReceivedNotificationSource"/> とは別の登録で、一覧に出さない種類も、行き先だけを登録できる。
/// </remarks>
public interface INotificationDestination
{
    /// <summary>種類のキー。通知の識別子の先頭と同じ</summary>
    string Key { get; }

    /// <summary>通知を開く画面の道を取得する</summary>
    /// <param name="id">元の行の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>開く画面の道。ブラウザのアドレスの道で、先頭は「/」。開けないときは null</returns>
    Task<string?> GetPathAsync(int id, CancellationToken cancellationToken = default);
}
