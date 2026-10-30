using System.Text.Json;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Messages.Client;

/// <summary>
/// 人の一覧と、更新の知らせを持つ。会話の画面と、人の一覧の画面で共有する。
/// </summary>
/// <param name="client">サーバーのメッセージの API。</param>
/// <param name="browser">ブラウザの部品。端末の控えと、更新の知らせに使う。</param>
/// <remarks>
/// サーバーが唯一の正で、端末の控えは、いつでも作り直せる。人の一覧は、まず控えから出し、そのあと、サーバーに前回からの変更を聞いて直す。
/// 会話を開いている間は、サーバーからの更新の知らせ (Server-Sent Events)を受けて、差分を取る。
/// 同じ行が何度届いても、番号と更新日時で突き合わせるので、結果は変わらない。
/// </remarks>
public sealed class MessageStore(MessagesClient client, BrowserClient browser) : IAsyncDisposable
{
    /// <summary>人の一覧の控えの名前。</summary>
    private const string ListCacheKey = "messages.list";

    /// <summary>読んでいない通知の種類。通知の識別子「message:番号」の、頭の部分。</summary>
    internal const string MessageKind = "message";

    /// <summary>更新の知らせの道。</summary>
    private const string EventsPath = "/api/messages/events";

    /// <summary>会話ごとの控えの名前の先頭。</summary>
    private const string ConversationCachePrefix = "messages.c.";

    /// <summary>更新の知らせを受けている間の、使っている画面の数。</summary>
    private int _liveCount;

    /// <summary>更新の知らせの購読。</summary>
    private IAsyncDisposable? _events;

    /// <summary>差分の取得を、同時に 2 つ走らせないための鍵。</summary>
    private readonly SemaphoreSlim _syncing = new(1, 1);

    /// <summary>自分の番号。まだ読んでいなければ 0。</summary>
    public int UserId { get; private set; }

    /// <summary>次の差分の取得に送る基準の日時。まだ読んでいなければ null。</summary>
    public DateTimeOffset? NextBase { get; private set; }

    /// <summary>会話の要約。最後のメッセージが新しい順。</summary>
    public IReadOnlyList<ConversationDto> Conversations { get; private set; } = [];

    /// <summary>読んでいないメッセージの番号。端末ごとに持つ。会話の行の印と、ホーム画面のアイコンの数字の元。</summary>
    public IReadOnlySet<int> UnreadMessageIds { get; private set; } = new HashSet<int>();

    /// <summary>いま開いている会話の番号。開いていなければ 0。開いている会話の新着は、読んでいないものに数えない。</summary>
    public int OpenConversationId { get; set; }

    /// <summary>会話の要約が変わった。</summary>
    public event Func<Task>? ConversationsChanged;

    /// <summary>
    /// 会話に、読んでいないメッセージがあるか。
    /// </summary>
    /// <param name="conversation">会話の要約。</param>
    /// <returns>最後のメッセージが読んでいないとき true。会話を開くと、画面に出たメッセージが読んだことになるので、最後のメッセージで決める。</returns>
    public bool IsUnread(ConversationDto conversation) => UnreadMessageIds.Contains(conversation.LastMessageId);

    /// <summary>
    /// サーバーから読み直した、相手が受け取れるかを、会話の要約と控えへ反映する。
    /// </summary>
    /// <param name="conversationId">会話の番号。</param>
    /// <param name="peerCanReceive">相手が受け取り先を指定しているか。</param>
    /// <returns>反映したことを表すタスク。要約に無い会話と、値が同じときは、何もしない。</returns>
    /// <remarks>差分の取得は、新しいメッセージのあった会話の要約しか返さないので、相手が受け取り先を変えても、要約は古いままになるため。</remarks>
    public async Task UpdatePeerCanReceiveAsync(int conversationId, bool peerCanReceive)
    {
        if (Conversations.FirstOrDefault(conversation => conversation.Id == conversationId) is not { } current || current.PeerCanReceive == peerCanReceive)
        {
            return;
        }

        Conversations = [.. Conversations.Select(conversation => conversation.Id == conversationId ? conversation with { PeerCanReceive = peerCanReceive } : conversation)];
        await WriteListCacheAsync();
        await RaiseConversationsChangedAsync();
    }

    /// <summary>
    /// 画面に出したメッセージを、読んだことにする。
    /// </summary>
    /// <param name="shown">画面に出したメッセージ。</param>
    /// <returns>読んだことにしたことを表すタスク。</returns>
    public async Task MarkReadAsync(IEnumerable<MessageDto> shown)
    {
        var ids = shown.Where(message => UnreadMessageIds.Contains(message.Id)).Select(message => message.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await browser.RemoveUnreadAsync(MessageKind, ids);
        await RefreshUnreadAsync();
        await RaiseConversationsChangedAsync();
    }

    /// <summary>
    /// 読んでいないメッセージを、すべて読んだことにする。
    /// </summary>
    /// <returns>読んだことにしたことを表すタスク。</returns>
    public async Task MarkAllReadAsync()
    {
        await browser.ClearUnreadAsync(MessageKind);
        await RefreshUnreadAsync();
        await RaiseConversationsChangedAsync();
    }

    /// <summary>メッセージが届いた、または更新された。会話の画面が、開いている会話の分を取り込む。</summary>
    public event Func<IReadOnlyList<MessageDto>, Task>? MessagesArrived;

    /// <summary>
    /// 人の一覧を読み込む。まず控えから出し、そのあとサーバーから読む。
    /// </summary>
    /// <returns>サーバーから読んだ結果。控えだけで終わったときは、サーバーの失敗。</returns>
    public async Task<ApiResult<bool>> LoadAsync()
    {
        if (NextBase is null && await ReadListCacheAsync() is { } cached)
        {
            UserId = cached.UserId;
            NextBase = cached.NextBase;
            Conversations = cached.Conversations;
            await RefreshUnreadAsync();
            await RaiseConversationsChangedAsync();
        }

        return await SyncAsync();
    }

    /// <summary>
    /// サーバーから、前回からの変更を読んで、取り込む。前回が無いときは、最初の読み込み。
    /// </summary>
    /// <returns>結果。失敗のとき、控えの内容はそのまま残す。</returns>
    public async Task<ApiResult<bool>> SyncAsync()
    {
        await _syncing.WaitAsync();
        try
        {
            var result = NextBase is { } known ? await client.LoadChangesAsync(known) : await client.LoadStartAsync();
            if (!result.Succeeded)
            {
                return new ApiResult<bool>(false, result.Message, result.Status);
            }

            var sync = result.Value!;
            if (UserId != 0 && UserId != sync.UserId)
            {
                // 控えの持ち主と、いまの人が違う。控えを捨てて、最初から読み直す。鍵を持ったまま、ここで読む
                await browser.ClearCacheAsync();
                UserId = 0;
                NextBase = null;
                Conversations = [];
                result = await client.LoadStartAsync();
                if (!result.Succeeded)
                {
                    await RaiseConversationsChangedAsync();
                    return new ApiResult<bool>(false, result.Message, result.Status);
                }
                sync = result.Value!;
            }

            UserId = sync.UserId;
            var isStart = NextBase is null;
            NextBase = sync.NextBase;
            Conversations = isStart ? sync.Conversations : Merge(Conversations, sync.Conversations);
            await WriteListCacheAsync();
            await TrackUnreadAsync(isStart, sync.Messages);
            await RaiseConversationsChangedAsync();
            if (sync.Messages.Count > 0 && MessagesArrived is { } arrived)
            {
                await arrived(sync.Messages);
            }
            return new ApiResult<bool>(true, null, ApiFailure.None);
        }
        finally
        {
            _syncing.Release();
        }
    }

    /// <summary>
    /// 更新の知らせを受け始める。会話を開いている間だけ使う。
    /// </summary>
    /// <returns>閉じると、使い終わったことを伝える控え。使っている画面がなくなると、つなぎをやめる。</returns>
    public async Task<IAsyncDisposable> AcquireLiveAsync()
    {
        if (Interlocked.Increment(ref _liveCount) == 1)
        {
            _events = await browser.OpenEventsAsync(EventsPath, () => SyncAsync());
        }
        return new LiveLease(this);
    }

    /// <summary>
    /// 会話の控えを読む。
    /// </summary>
    /// <param name="conversationId">会話の番号。</param>
    /// <returns>控えのメッセージ。古いものが先。無い・別の人の控え・読めないときは空。</returns>
    public async Task<IReadOnlyList<MessageDto>> ReadConversationCacheAsync(int conversationId)
    {
        var text = await browser.CacheGetAsync(ConversationCachePrefix + conversationId);
        if (text is null)
        {
            return [];
        }

        try
        {
            var state = JsonSerializer.Deserialize(text, MessagesJsonContext.Default.ConversationCacheState);
            return state is not null && UserId != 0 && state.UserId == UserId ? state.Messages : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// 会話の控えを書く。新しいほうから、上限の件数までを、切れ目なく残す。
    /// </summary>
    /// <param name="conversationId">会話の番号。</param>
    /// <param name="messages">いま見えているメッセージ。古いものが先。</param>
    /// <returns>書いたことを表すタスク。</returns>
    public Task WriteConversationCacheAsync(int conversationId, IReadOnlyList<MessageDto> messages)
    {
        var recent = messages.Count > CacheLimit ? messages.Skip(messages.Count - CacheLimit).ToList() : messages.ToList();
        var state = new ConversationCacheState(UserId, conversationId, recent);
        return browser.CachePutAsync(ConversationCachePrefix + conversationId, JsonSerializer.Serialize(state, MessagesJsonContext.Default.ConversationCacheState));
    }

    /// <summary>端末の控えに残す、会話ごとのメッセージの件数。仮。</summary>
    public const int CacheLimit = 200;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_events is not null)
        {
            await _events.DisposeAsync();
        }
        _syncing.Dispose();
    }

    /// <summary>
    /// 使い終わった画面が、更新の知らせを手放す。
    /// </summary>
    /// <returns>手放したことを表すタスク。</returns>
    private async ValueTask ReleaseLiveAsync()
    {
        if (Interlocked.Decrement(ref _liveCount) == 0 && _events is { } events)
        {
            _events = null;
            await events.DisposeAsync();
        }
    }

    /// <summary>
    /// 取り込んだ新着を、読んでいないメッセージに数え、一覧を読み直す。
    /// </summary>
    /// <param name="isStart">最初の読み込みか。最初は、いまあるメッセージを数えず、基準の番号だけを置く。</param>
    /// <param name="messages">取り込んだメッセージ。</param>
    /// <returns>数えたことを表すタスク。</returns>
    /// <remarks>自分が受け取っていないメッセージと、いま開いている会話のメッセージは、数えない。基準の番号は、数えないものも含めて進める。</remarks>
    private async Task TrackUnreadAsync(bool isStart, IReadOnlyList<MessageDto> messages)
    {
        if (isStart)
        {
            await browser.InitUnreadAsync(MessageKind, Conversations.Count == 0 ? 0 : Conversations.Max(conversation => conversation.LastMessageId));
        }
        if (messages.Count > 0)
        {
            var incoming = messages
                .Where(message => IsReceived(message) && message.ConversationId != OpenConversationId)
                .Select(message => message.Id)
                .ToList();
            await browser.AddArrivedUnreadAsync(MessageKind, incoming, messages.Max(message => message.Id));
        }
        await RefreshUnreadAsync();
    }

    /// <summary>
    /// 自分が受け取るメッセージか。
    /// </summary>
    /// <param name="message">メッセージ。</param>
    /// <returns>他の人が送ったメッセージ、または自分との会話のメッセージのとき true。自分との会話は、送ったメッセージが自分にも届くため。</returns>
    private bool IsReceived(MessageDto message)
        => !message.IsMine
        || Conversations.Any(conversation => conversation.Id == message.ConversationId && conversation.IsDirect && conversation.PeerUserId == UserId);

    /// <summary>
    /// 読んでいないメッセージの番号を、端末の控えから読み直す。
    /// </summary>
    /// <returns>読み直したことを表すタスク。</returns>
    private async Task RefreshUnreadAsync()
    {
        const string prefix = MessageKind + ":";
        var ids = new HashSet<int>();
        foreach (var key in await browser.GetUnreadAsync())
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(key.AsSpan(prefix.Length), out var id))
            {
                ids.Add(id);
            }
        }
        UnreadMessageIds = ids;
    }

    /// <summary>
    /// 人の一覧の変わりを知らせる。
    /// </summary>
    /// <returns>知らせ終えたことを表すタスク。</returns>
    private Task RaiseConversationsChangedAsync() => ConversationsChanged?.Invoke() ?? Task.CompletedTask;

    /// <summary>
    /// 人の一覧の控えを読む。
    /// </summary>
    /// <returns>控え。無い・読めないときは null。</returns>
    private async Task<MessageCacheState?> ReadListCacheAsync()
    {
        var text = await browser.CacheGetAsync(ListCacheKey);
        if (text is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(text, MessagesJsonContext.Default.MessageCacheState);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 人の一覧の控えを書く。
    /// </summary>
    /// <returns>書いたことを表すタスク。</returns>
    private Task WriteListCacheAsync()
        => browser.CachePutAsync(
            ListCacheKey,
            JsonSerializer.Serialize(new MessageCacheState(UserId, NextBase ?? DateTimeOffset.MinValue, Conversations), MessagesJsonContext.Default.MessageCacheState));

    /// <summary>
    /// 会話の要約を、番号で突き合わせて取り込み、最後のメッセージが新しい順に並べる。
    /// </summary>
    /// <param name="current">いまの要約。</param>
    /// <param name="changed">変わった要約。</param>
    /// <returns>取り込んだあとの要約。</returns>
    private static IReadOnlyList<ConversationDto> Merge(IReadOnlyList<ConversationDto> current, IReadOnlyList<ConversationDto> changed)
    {
        if (changed.Count == 0)
        {
            return current;
        }

        var byId = current.ToDictionary(conversation => conversation.Id);
        foreach (var conversation in changed)
        {
            byId[conversation.Id] = conversation;
        }
        return [.. byId.Values.OrderByDescending(conversation => conversation.LastMessageId)];
    }

    /// <summary>更新の知らせを使っている画面の、手放しを受ける控え。</summary>
    /// <param name="store">持ち主。</param>
    private sealed class LiveLease(MessageStore store) : IAsyncDisposable
    {
        /// <summary>手放し済みか。</summary>
        private int _released;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => Interlocked.Exchange(ref _released, 1) == 0 ? store.ReleaseLiveAsync() : ValueTask.CompletedTask;
    }
}
