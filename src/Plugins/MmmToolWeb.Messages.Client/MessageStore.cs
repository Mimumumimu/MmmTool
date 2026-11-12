using System.Text.Json;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Messages.Client;

/// <summary>人の一覧と更新の知らせを持つ</summary>
/// <param name="client">サーバーのメッセージの API</param>
/// <param name="browser">
/// ブラウザの部品。
/// 端末の控えと、更新の知らせに使う。
/// </param>
/// <remarks>
/// 会話の画面と人の一覧の画面で共有する。
/// サーバーが唯一の正で、端末の控えはいつでも作り直せる。
/// 人の一覧はまず控えから出し、そのあと、サーバーに前回からの変更を聞いて直す。
/// 会話を開いている間はサーバーからの更新の知らせ (Server-Sent Events)を受けて、差分を取る。
/// 同じ行が何度届いても、番号と更新日時で突き合わせるので、結果は変わらない。
/// </remarks>
public sealed class MessageStore(MessagesClient client, BrowserClient browser) : IAsyncDisposable
{
    /// <summary>人の一覧の控えの名前</summary>
    private const string ListCacheKey = "messages.list";

    /// <summary>読んでいない通知の種類</summary>
    /// <remarks>通知の識別子「message:番号」の頭の部分</remarks>
    internal const string MessageKind = "message";

    /// <summary>更新の知らせの道</summary>
    private const string EventsPath = "/api/messages/events";

    /// <summary>会話ごとの控えの名前の先頭</summary>
    private const string ConversationCachePrefix = "messages.c.";

    /// <summary>更新の知らせを受けている間の使っている画面の数</summary>
    private int _liveCount;

    /// <summary>更新の知らせの購読</summary>
    private IAsyncDisposable? _events;

    /// <summary>差分の取得を同時に 2 つ走らせないための鍵</summary>
    private readonly SemaphoreSlim _syncing = new(1, 1);

    /// <summary>自分の番号</summary>
    /// <remarks>まだ読んでいなければ 0</remarks>
    public int UserId { get; private set; }

    /// <summary>作る途中のグループ</summary>
    /// <remarks>
    /// 名前と参加者を決めてから、最初の送信までの間だけ持つ。
    /// 会話は最初の送信で作る。
    /// ページを読み直すと消えるので、空のグループの画面は一覧へ戻る。
    /// </remarks>
    public PendingGroup? PendingGroup { get; set; }

    /// <summary>次の差分の取得に送る基準の日時</summary>
    /// <remarks>まだ読んでいなければ null</remarks>
    public DateTimeOffset? NextBase { get; private set; }

    /// <summary>会話の要約</summary>
    /// <remarks>最後のメッセージが新しい順</remarks>
    public IReadOnlyList<ConversationDto> Conversations { get; private set; } = [];

    /// <summary>いま開いている会話の番号</summary>
    /// <remarks>開いていなければ 0</remarks>
    public int OpenConversationId { get; set; }

    /// <summary>会話の要約が変わった</summary>
    public event Func<Task>? ConversationsChanged;

    /// <summary>自分が会話を抜けたことが届いた</summary>
    /// <remarks>
    /// 引数は会話の番号
    /// 開いている画面が一覧へ戻るために受ける
    /// </remarks>
    public event Func<int, Task>? ConversationLeft;

    /// <summary>端末から書いた会話ごとの読んだ位置の最大</summary>
    /// <remarks>同じ位置を何度も書かないため</remarks>
    private readonly Dictionary<int, int> _reportedRead = [];

    /// <summary>会話に読んでいないメッセージがあるか</summary>
    /// <param name="conversation">会話の要約</param>
    /// <returns>
    /// サーバーが数えた、読んでいないメッセージの数が 1 以上のとき true。
    /// 読んだ位置は DB にあるので、端末ごとの状態は持たない。
    /// </returns>
    public bool IsUnread(ConversationDto conversation) => conversation.UnreadCount > 0;

    /// <summary>サーバーから読み直した、相手が受け取れるかを会話の要約と控えへ反映する</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="peerNotifiable">相手が通知先を指定しているか</param>
    /// <returns>
    /// 反映したことを表すタスク。
    /// 要約に無い会話と、値が同じときは何もしない。
    /// </returns>
    /// <remarks>差分の取得は新しいメッセージのあった会話の要約しか返さないので、相手が通知先を変えても、要約は古いままになるため。</remarks>
    public async Task UpdatePeerNotifiableAsync(int conversationId, bool peerNotifiable)
    {
        if (Conversations.FirstOrDefault(conversation => conversation.Id == conversationId) is not { } current || current.PeerNotifiable == peerNotifiable)
        {
            return;
        }

        Conversations = [.. Conversations.Select(conversation => conversation.Id == conversationId ? conversation with { PeerNotifiable = peerNotifiable } : conversation)];
        await WriteListCacheAsync();
        await RaiseConversationsChangedAsync();
    }

    /// <summary>画面に出したメッセージを読んだことにする</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="shown">画面に出したメッセージ</param>
    /// <returns>
    /// 書いたことを表すタスク。
    /// 画面が前面に見えていないとき、読んだ位置がすでに同じか大きいとき、書けなかったときは、何も変えない。
    /// </returns>
    /// <remarks>
    /// 読んだ位置を DB に書く。
    /// 読んだ位置は相手の画面に「既読」を出す元で、自分の未読の数の元でもある。
    /// 画面が裏に回っている間に届いたメッセージは前面に戻ってから、読んだことになる。
    /// </remarks>
    public async Task MarkReadAsync(int conversationId, IEnumerable<MessageDto> shown)
    {
        var newest = shown.Select(message => message.Id).DefaultIfEmpty(0).Max();
        if (conversationId <= 0 || newest <= 0 || newest <= _reportedRead.GetValueOrDefault(conversationId) || !await browser.IsVisibleAsync())
        {
            return;
        }

        var result = await client.MarkReadAsync(conversationId, newest);
        if (!result.Succeeded)
        {
            return;
        }

        _reportedRead[conversationId] = newest;
        if (Conversations.FirstOrDefault(conversation => conversation.Id == conversationId) is { } current && current.UnreadCount > 0 && newest >= current.LastMessageId)
        {
            Conversations = [.. Conversations.Select(conversation => conversation.Id == conversationId ? conversation with { UnreadCount = 0 } : conversation)];
            await WriteListCacheAsync();
            await ShowBadgeAsync();
            await RaiseConversationsChangedAsync();
        }
    }

    /// <summary>読んでいないメッセージの合計をホーム画面のアイコンの数字に反映する</summary>
    /// <returns>反映したことを表すタスク</returns>
    private Task ShowBadgeAsync() => browser.SetServerUnreadAsync(MessageKind, Conversations.Sum(conversation => conversation.UnreadCount));

    /// <summary>メッセージが届いた、または更新された</summary>
    /// <remarks>会話の画面が開いている会話の分を取り込む</remarks>
    public event Func<IReadOnlyList<MessageDto>, Task>? MessagesArrived;

    /// <summary>人の一覧を読み込む</summary>
    /// <returns>
    /// サーバーから読んだ結果
    /// 控えだけで終わったときはサーバーの失敗
    /// </returns>
    /// <remarks>まず控えから出し、そのあとサーバーから読む。</remarks>
    public async Task<ApiResult<bool>> LoadAsync()
    {
        await RestoreAsync();
        return await SyncAsync();
    }

    /// <summary>人の一覧を端末の控えだけから戻す</summary>
    /// <returns>
    /// 戻したことを表すタスク。
    /// すでに読み込み済み、または控えが無いときは何もしない。
    /// </returns>
    /// <remarks>
    /// サーバーとは通信しない。
    /// 会話を直接開いたとき、サーバーの応答を待たずに、会話の控えを読むため。
    /// 控えは持ち主の番号と突き合わせるので、先に自分の番号が要る。
    /// </remarks>
    public async Task RestoreAsync()
    {
        if (NextBase is null && await ReadListCacheAsync() is { } cached)
        {
            UserId = cached.UserId;
            NextBase = cached.NextBase;
            Conversations = cached.Conversations;
            await RaiseConversationsChangedAsync();
        }
    }

    /// <summary>サーバーから前回からの変更を読んで、取り込む</summary>
    /// <returns>
    /// 結果。
    /// 失敗のとき、控えの内容はそのまま残す。
    /// </returns>
    /// <remarks>前回が無いときは最初の読み込み</remarks>
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
                _reportedRead.Clear();
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
            IReadOnlyList<ConversationDto> previous = isStart ? [] : Conversations;
            NextBase = sync.NextBase;
            Conversations = isStart ? sync.Conversations : Merge(Conversations, sync.Conversations);
            foreach (var leftId in sync.LeftConversationIds)
            {
                // 別の端末で抜けたグループ。一覧と控えから消す。入り直したときに、古い控えと読み直した分の間に、抜けが出ないようにする
                Conversations = [.. Conversations.Where(conversation => conversation.Id != leftId)];
                await WriteConversationCacheAsync(leftId, []);
            }
            await WriteListCacheAsync();
            await ShowBadgeAsync();
            await RaiseConversationsChangedAsync();
            foreach (var leftId in sync.LeftConversationIds)
            {
                if (ConversationLeft is { } left)
                {
                    await left(leftId);
                }
            }
            await MergeIntoConversationCachesAsync(sync.Messages, previous);
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

    /// <summary>更新の知らせを受け始める</summary>
    /// <returns>
    /// 閉じると、使い終わったことを伝える控え。
    /// 使っている画面がなくなると、つなぎをやめる。
    /// </returns>
    /// <remarks>会話を開いている間だけ使う</remarks>
    public async Task<IAsyncDisposable> AcquireLiveAsync()
    {
        if (Interlocked.Increment(ref _liveCount) == 1)
        {
            _events = await browser.OpenEventsAsync(EventsPath, () => SyncAsync());
        }
        return new LiveLease(this);
    }

    /// <summary>会話の控えを読む</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <returns>
    /// 控えのメッセージ
    /// 古いものが先
    /// 無い・別の人の控え・読めないときは空
    /// </returns>
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

    /// <summary>会話の控えを書く</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="messages">
    /// いま見えているメッセージ
    /// 古いものが先
    /// </param>
    /// <returns>書いたことを表すタスク</returns>
    /// <remarks>新しいほうから、上限の件数までを、切れ目なく残す。</remarks>
    public Task WriteConversationCacheAsync(int conversationId, IReadOnlyList<MessageDto> messages)
    {
        var recent = messages.Count > CacheLimit ? messages.Skip(messages.Count - CacheLimit).ToList() : messages.ToList();
        var state = new ConversationCacheState(UserId, conversationId, recent);
        return browser.CachePutAsync(ConversationCachePrefix + conversationId, JsonSerializer.Serialize(state, MessagesJsonContext.Default.ConversationCacheState));
    }

    /// <summary>差分で届いたメッセージをすでに控えのある会話の控えへ足す</summary>
    /// <param name="messages">差分で届いた、または更新されたメッセージ。</param>
    /// <param name="previous">今回の差分を取り込む前の、会話の要約。</param>
    /// <returns>書いたことを表すタスク</returns>
    /// <remarks>
    /// 会話の画面を開く前に控えを最新にしておくため。
    /// 差分は前回の基準より後の変更を、すべて返す。
    /// 足すのは控えの最後のメッセージが前回の要約の最後のメッセージと同じ会話だけ。
    /// 前回の時点で、控えが最新だった証拠がこれしかないため。
    /// 違う会話 (控えの無い会話・前回以降に取りこぼした会話)は足すと途中が抜けるので書かず、開いたときにサーバーから読み直す。
    /// 控えの先頭より古い番号のメッセージは足さない。
    /// 控えは新しいほうから切れ目なく持つため。
    /// 開いている会話は会話の画面が届いたメッセージを持つので、ここでは書かない。
    /// 同じメッセージが何度届いても、番号と更新日時で突き合わせるので、結果は変わらない。
    /// </remarks>
    private async Task MergeIntoConversationCachesAsync(IReadOnlyList<MessageDto> messages, IReadOnlyList<ConversationDto> previous)
    {
        foreach (var group in messages.GroupBy(message => message.ConversationId))
        {
            if (group.Key == OpenConversationId || previous.FirstOrDefault(conversation => conversation.Id == group.Key) is not { } before)
            {
                continue;
            }

            var cached = await ReadConversationCacheAsync(group.Key);
            if (cached.Count == 0 || cached[^1].Id != before.LastMessageId)
            {
                continue;
            }

            var oldest = cached[0].Id;
            var byId = cached.ToDictionary(message => message.Id);
            foreach (var message in group.Where(message => message.Id >= oldest))
            {
                if (!byId.TryGetValue(message.Id, out var existing) || message.UpdatedAt >= existing.UpdatedAt)
                {
                    byId[message.Id] = message;
                }
            }
            await WriteConversationCacheAsync(group.Key, [.. byId.Values.OrderBy(message => message.Id)]);
        }
    }

    /// <summary>会話ごとの控えの件数</summary>
    /// <remarks>端末の控えに残す、会話ごとのメッセージの件数。</remarks>
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

    /// <summary>使い終わった画面が更新の知らせを手放す</summary>
    /// <returns>手放したことを表すタスク</returns>
    private async ValueTask ReleaseLiveAsync()
    {
        if (Interlocked.Decrement(ref _liveCount) == 0 && _events is { } events)
        {
            _events = null;
            await events.DisposeAsync();
        }
    }

    /// <summary>人の一覧の変わりを知らせる</summary>
    /// <returns>知らせ終えたことを表すタスク</returns>
    private Task RaiseConversationsChangedAsync() => ConversationsChanged?.Invoke() ?? Task.CompletedTask;

    /// <summary>人の一覧の控えを読む</summary>
    /// <returns>
    /// 控え
    /// 無い・読めないときは null
    /// </returns>
    private async Task<MessageCacheState?> ReadListCacheAsync()
    {
        var text = await browser.CacheGetAsync(ListCacheKey);
        if (text is null)
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize(text, MessagesJsonContext.Default.MessageCacheState);
            return state is { Version: MessageCacheState.CurrentVersion } ? state : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>人の一覧の控えを書く</summary>
    /// <returns>書いたことを表すタスク</returns>
    private Task WriteListCacheAsync()
        => browser.CachePutAsync(
            ListCacheKey,
            JsonSerializer.Serialize(MessageCacheState.Create(UserId, NextBase ?? DateTimeOffset.MinValue, Conversations), MessagesJsonContext.Default.MessageCacheState));

    /// <summary>会話の要約を番号で突き合わせて取り込み、最後のメッセージが新しい順に並べる</summary>
    /// <param name="current">いまの要約</param>
    /// <param name="changed">変わった要約</param>
    /// <returns>取り込んだあとの要約</returns>
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

    /// <summary>更新の知らせを使っている画面の手放しを受ける控え</summary>
    /// <param name="store">持ち主</param>
    private sealed class LiveLease(MessageStore store) : IAsyncDisposable
    {
        /// <summary>手放し済みか</summary>
        private int _released;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => Interlocked.Exchange(ref _released, 1) == 0 ? store.ReleaseLiveAsync() : ValueTask.CompletedTask;
    }
}
