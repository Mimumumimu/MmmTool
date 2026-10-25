using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace MmmToolWeb.Messages;

/// <summary>
/// 会話の更新を、会話を開いている端末に知らせる口。端末ごとに 1 本の流れを持つ。
/// </summary>
/// <remarks>
/// 知らせる中身は「更新がある」だけ。端末は、知らせを受けたら、差分の取得で新しいメッセージを読む。
/// 流れが詰まっても、知らせは 1 つにまとめる。端末が遅れても、メモリを増やさないため。
/// 接続していない人の分は、何も持たない。
/// </remarks>
public sealed class MessageChangeHub
{
    /// <summary>生きていることを伝える合図の間隔。通信の途中で切られないようにする</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(25);

    /// <summary>接続中の流れ。人の番号ごと</summary>
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Channel<bool>>> _subscribers = new();

    /// <summary>接続している端末があるか</summary>
    public bool HasSubscribers => !_subscribers.IsEmpty;

    /// <summary>更新を知らせる相手に、知らせる</summary>
    /// <param name="userIds">更新のあった会話に入っている人の番号</param>
    public void Notify(IEnumerable<int> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        foreach (var userId in userIds)
        {
            if (!_subscribers.TryGetValue(userId, out var channels))
            {
                continue;
            }
            foreach (var channel in channels.Values)
            {
                channel.Writer.TryWrite(true);
            }
        }
    }

    /// <summary>1 人の端末の流れを始める</summary>
    /// <param name="userId">人の番号</param>
    /// <param name="cancellationToken">端末が切れたときに止まるトークン</param>
    /// <returns>知らせの流れ。「changed」は更新の知らせで、「ping」は生きている合図</returns>
    public async IAsyncEnumerable<SseItem<string>> SubscribeAsync(int userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        var channels = _subscribers.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<bool>>());
        channels[id] = channel;
        try
        {
            // つないだ直後に 1 回知らせる。つなぐ前に届いた分を、端末が読み直すため
            yield return new SseItem<string>("", "changed");
            while (!cancellationToken.IsCancellationRequested)
            {
                using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                heartbeat.CancelAfter(HeartbeatInterval);
                var changed = false;
                try
                {
                    changed = await channel.Reader.ReadAsync(heartbeat.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // 合図の間隔が来た
                }
                yield return new SseItem<string>("", changed ? "changed" : "ping");
            }
        }
        finally
        {
            channels.TryRemove(id, out _);
            if (channels.IsEmpty)
            {
                _subscribers.TryRemove(new KeyValuePair<int, ConcurrentDictionary<Guid, Channel<bool>>>(userId, channels));
            }
        }
    }
}
