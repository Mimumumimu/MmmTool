using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Messages;
using MmmTool.Messages.Core;

namespace MmmToolWeb.Messages;

/// <summary>
/// 数秒おきに、DB の更新されたメッセージを調べて、会話を開いている端末に知らせる常駐の処理。
/// </summary>
/// <remarks>
/// 全員分を 1 つの問い合わせにして、索引で、基準より後に更新された行だけを読む。接続している端末が無いときは、読まない。
/// 基準から <see cref="MessageSync.Lookback"/> だけ戻して読むので、同じ行を何度か読む。同じ行は、番号と更新日時を覚えて、2 度目からは知らせない。
/// MmmTool (PC 版)が DB に直接書いたメッセージも、DB を読んで気づける。
/// </remarks>
/// <param name="reader">更新を読む部品</param>
/// <param name="hub">知らせる口</param>
/// <param name="logger">ログ</param>
public sealed class MessageChangeWatcher(SqlServerMessageChangeReader reader, MessageChangeHub hub, ILogger<MessageChangeWatcher> logger) : BackgroundService
{
    /// <summary>DB を調べる間隔。仮</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    /// <summary>知らせ済みの印を残す時間。戻して読む時間より長くする</summary>
    private static readonly TimeSpan RememberTime = MessageSync.Lookback + TimeSpan.FromSeconds(20);

    /// <summary>知らせ済みの、更新された行の種類と番号と更新日時と、知らせた相手。古いものから捨てる</summary>
    private readonly Dictionary<(bool IsReadPosition, int RowId, DateTimeOffset UpdatedAt, int UserId), DateTimeOffset> _seen = [];

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? baseTime = null;
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                if (!hub.HasSubscribers)
                {
                    // 誰も接続していない間の更新は、端末がつないだときの読み直しで拾う
                    baseTime = null;
                    continue;
                }

                try
                {
                    // 基準は、DB の時計の日時。まだ読んでいないときは、少し前から読む。つないだ直後の知らせで、端末が読み直すので、取りこぼさない
                    var since = baseTime is { } known ? MessageSync.SinceOf(known) : DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30);
                    var (changes, now) = await reader.ReadAsync(since, stoppingToken).ConfigureAwait(false);
                    Notify(changes, now);
                    baseTime = now;
                }
                catch (DataFileException ex)
                {
                    // 読めなかったときは、基準を進めず、次の回にもう一度読む
                    logger.LogWarning("メッセージの更新を調べられませんでした: {Message}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 止めるとき
        }
    }

    /// <summary>まだ知らせていない更新を、参加者に知らせる</summary>
    /// <param name="changes">読んだ更新</param>
    /// <param name="now">DB の今の日時</param>
    private void Notify(IReadOnlyList<MessageChange> changes, DateTimeOffset now)
    {
        var users = new HashSet<int>();
        foreach (var change in changes)
        {
            if (_seen.TryAdd((change.IsReadPosition, change.RowId, change.UpdatedAt, change.UserId), now))
            {
                users.Add(change.UserId);
            }
        }
        hub.Notify(users);

        foreach (var key in _seen.Where(pair => now - pair.Value > RememberTime).Select(pair => pair.Key).ToList())
        {
            _seen.Remove(key);
        }
    }
}
