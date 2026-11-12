using MmmSdk.Core.Components.Scheduling;
using MmmSdk.Core.Components.Storage;
using MmmTool.Messages.Cache;
using MmmTool.Messages.Core;
using MmmTool.Users.Core;

namespace MmmTool.Messages;

/// <summary>会話とメッセージを DB から読み続ける</summary>
/// <remarks>
/// 人の一覧と、開いている会話の新着を、画面と通知ダイアログに渡す。
/// サーバーを通さず、PC が DB を、場面ごとの間隔で読む。
/// 会話を開いていて、ウィンドウが前面にあるときは 2 秒、それ以外でアプリが動いている間は 10 秒。
/// 読む時刻はアプリ全体で 1 つの <see cref="ITicker"/> の間隔の合図 (<see cref="TickTiming.Interval"/>)で決める。
/// 自分ではタイマーを持たない。
/// 読むのはどの場面も同じ差分の取得で、会話の数に関係なく、索引 4 回。
/// 人の一覧は読んだ結果から作り直す。
/// 起動直後はまず端末の控え (<see cref="MessageCacheStore"/>)から出し、続けて差分を読む。
/// 控えはいつでも作り直せる。
/// 読んだ結果は任意のスレッドから通知する。
/// 画面は UI スレッドに切り替えて使う。
/// </remarks>
/// <param name="repository">会話とメッセージの保存先</param>
/// <param name="currentUser">今のユーザー</param>
/// <param name="cache">端末の控え</param>
/// <param name="ticker">定期処理の合図</param>
public sealed class MessageSyncService(IMessageRepository repository, CurrentUser currentUser, MessageCacheStore cache, ITicker ticker) : IDisposable
{
    /// <summary>前面にあるときの間隔</summary>
    /// <remarks>会話を開いていて、ウィンドウが前面にあるときの間隔。</remarks>
    private static readonly TimeSpan FrontInterval = TimeSpan.FromSeconds(2);

    /// <summary>それ以外の間隔</summary>
    /// <remarks>アプリが動いている間の間隔</remarks>
    private static readonly TimeSpan BackgroundInterval = TimeSpan.FromSeconds(10);

    /// <summary>読む処理を同時に 2 つ走らせないための鍵</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>通知ダイアログに出した他の人のメッセージの番号</summary>
    /// <remarks>同じメッセージを続けて出さない</remarks>
    private readonly HashSet<int> _notified = [];

    /// <summary>この PC から DB に書いた会話ごとの読んだ位置の最大</summary>
    /// <remarks>同じ位置を何度も書かないため</remarks>
    private readonly Dictionary<int, int> _reportedRead = [];

    /// <summary>読む処理を止めるための取り消し</summary>
    /// <remarks>破棄で取り消す</remarks>
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>読む時刻の合図の登録</summary>
    /// <remarks>
    /// Start の前は null
    /// アプリの起動時に 1 回だけ入れる
    /// 画面の状態を伝える側は入る前なら何もしない
    /// </remarks>
    private volatile ITickSubscription? _subscription;

    /// <summary>いま開いている会話の番号</summary>
    /// <remarks>開いていなければ 0</remarks>
    private int _openConversationId;

    /// <summary>ウィンドウが前面にあるか</summary>
    private bool _inFront;

    /// <summary>読んだ人の番号</summary>
    /// <remarks>まだ読んでいなければ 0</remarks>
    private int _userId;

    /// <summary>次の差分の取得の基準にする日時</summary>
    /// <remarks>まだ読んでいなければ null</remarks>
    private DateTimeOffset? _nextBase;

    /// <summary>起動して最初に読んだ日時</summary>
    /// <remarks>
    /// DB の時計
    /// これより前に送られたメッセージは通知ダイアログに出さない
    /// </remarks>
    private DateTimeOffset? _sessionStart;

    /// <summary>会話の要約</summary>
    /// <remarks>最後のメッセージが新しい順</remarks>
    private IReadOnlyList<ConversationSummary> _conversations = [];

    /// <summary>会話の要約</summary>
    /// <remarks>最後のメッセージが新しい順</remarks>
    public IReadOnlyList<ConversationSummary> Conversations => _conversations;

    /// <summary>読んだ人の番号</summary>
    /// <remarks>まだ読んでいなければ 0</remarks>
    public int UserId => _userId;

    /// <summary>最後の読み込みの失敗の理由</summary>
    /// <remarks>成功していれば null</remarks>
    public string? LastError { get; private set; }

    /// <summary>読み込みを 1 回終えた</summary>
    /// <remarks>
    /// 成功でも失敗でも発火する
    /// 任意のスレッドから発火する
    /// </remarks>
    public event Action? Synced;

    /// <summary>メッセージが届いた、または更新された</summary>
    /// <remarks>任意のスレッドから発火する</remarks>
    public event Action<IReadOnlyList<ChatMessage>>? MessagesArrived;

    /// <summary>他の人から届いた新着</summary>
    /// <remarks>
    /// 起動してから届いたもので、まだ通知していないものだけ。
    /// システムメッセージは含まない。
    /// 任意のスレッドから発火する。
    /// </remarks>
    public event Action<IReadOnlyList<ChatMessage>>? NewMessagesFromOthers;

    /// <summary>自分が会話を抜けたことが届いた</summary>
    /// <remarks>
    /// 引数は会話の番号
    /// 任意のスレッドから発火する
    /// </remarks>
    public event Action<int>? ConversationLeft;

    /// <summary>読み続ける合図の登録を始める</summary>
    /// <param name="isEnabled">
    /// 機能がオンか
    /// オフの間は読まない
    /// </param>
    /// <remarks>
    /// アプリの起動時に 1 回だけ呼ぶ。
    /// 登録した直後には読まず、最初の間隔のあとから読む。
    /// 予想外の失敗 (バグ)は <see cref="ITicker"/> がアプリの受け皿に渡す。
    /// 読めない失敗 (<see cref="DataFileException"/>)は <see cref="LastError"/> に残して、失敗として伝える。
    /// 失敗が続くと <see cref="ITicker"/> が次の合図までの時間を延ばす。
    /// 会話を開いたとき (<see cref="SetViewState"/>)は延ばしている間もすぐ読む。
    /// </remarks>
    public void Start(Func<bool> isEnabled)
    {
        ArgumentNullException.ThrowIfNull(isEnabled);

        if (_subscription is not null)
        {
            return;
        }

        var token = _cancellation.Token;
        _subscription = ticker.Subscribe(
            async _ => await TrySyncAsync(token).ConfigureAwait(false) ? TickResult.Done : TickResult.Failed,
            new TickOptions { Timing = TickTiming.Interval, Interval = IntervalFor(inFront: false), IsActive = isEnabled });
    }

    /// <summary>画面の状態を伝える</summary>
    /// <param name="openConversationId">
    /// いま開いている会話の番号
    /// 開いていなければ 0
    /// </param>
    /// <param name="inFront">ウィンドウが前面にあり、メッセージのページが見えているか。</param>
    /// <remarks>
    /// 間隔を決めるために使う。
    /// 会話を開いて前面になったときは待たずに、すぐ読む。
    /// </remarks>
    public void SetViewState(int openConversationId, bool inFront)
    {
        var becameActive = inFront && openConversationId > 0 && !(_inFront && _openConversationId == openConversationId);
        _openConversationId = openConversationId;
        _inFront = inFront;
        if (_subscription is not { } subscription)
        {
            return;
        }

        subscription.SetInterval(IntervalFor(inFront && openConversationId > 0));
        if (becameActive)
        {
            subscription.WakeNow();
        }
    }

    /// <summary>DB から読み直した、相手が受け取れるかを会話の要約と端末の控えへ反映する</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="peerNotifiable">相手が通知先を指定しているか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 反映の完了を表すタスク。
    /// 要約に無い会話と、値が同じときは何もしない。
    /// </returns>
    /// <remarks>差分の取得は新しいメッセージのあった会話の要約しか返さないので、相手が通知先を変えても、要約は古いままになるため。</remarks>
    public async Task UpdatePeerNotifiableAsync(int conversationId, bool peerNotifiable, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_conversations.FirstOrDefault(conversation => conversation.Id == conversationId) is not { } current || current.PeerNotifiable == peerNotifiable)
            {
                return;
            }

            _conversations = [.. _conversations.Select(conversation => conversation.Id == conversationId ? conversation with { PeerNotifiable = peerNotifiable } : conversation)];
            if (_nextBase is { } known)
            {
                await cache.WriteListAsync(CachedConversationList.Create(_userId, known, _conversations), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        Synced?.Invoke();
    }

    /// <summary>会話の自分が読んだ位置を DB に書き、会話の要約と端末の控えへ反映する</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="messageId">画面に出ている、一番新しいメッセージの番号。</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 書く処理の完了を表すタスク。
    /// すでに同じか大きい位置を書いたとき、読めないときは何もしない。
    /// </returns>
    /// <remarks>
    /// 相手の画面に「既読」が出る元で、自分の未読の数の元でもある。
    /// 呼ぶ側は会話を開いていて、ウィンドウが前面に見えているときだけ呼ぶ。
    /// 書けなかったとき (<see cref="DataFileException"/>)は次に呼ばれたときにもう一度書く。
    /// 画面には出さない。
    /// </remarks>
    public async Task MarkReadAsync(int conversationId, int messageId, CancellationToken cancellationToken = default)
    {
        if (conversationId <= 0 || messageId <= 0 || messageId <= _reportedRead.GetValueOrDefault(conversationId) || !repository.IsAvailable || !currentUser.IsIdentified)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MarkReadOutcome outcome;
            try
            {
                outcome = await repository.MarkReadAsync(conversationId, messageId, cancellationToken).ConfigureAwait(false);
            }
            catch (DataFileException)
            {
                return;
            }

            _reportedRead[conversationId] = messageId;
            if (outcome is MarkReadOutcome.NotMember or MarkReadOutcome.MessageInvalid
                || _conversations.FirstOrDefault(conversation => conversation.Id == conversationId) is not { UnreadCount: > 0 } current
                || messageId < current.LastMessageId)
            {
                return;
            }

            _conversations = [.. _conversations.Select(conversation => conversation.Id == conversationId ? conversation with { UnreadCount = 0 } : conversation)];
            if (_nextBase is { } known)
            {
                await cache.WriteListAsync(CachedConversationList.Create(_userId, known, _conversations), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        Synced?.Invoke();
    }

    /// <summary>いま開いている会話の番号</summary>
    /// <returns>
    /// 前面で見ている会話の番号
    /// 前面でなければ 0
    /// </returns>
    /// <remarks>通知ダイアログに前面で見ている会話の新着を出さないために使う</remarks>
    public int GetFrontConversationId() => _inFront ? _openConversationId : 0;

    /// <summary>差分を 1 回読む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 読む処理の完了を表すタスク
    /// 読めなかったときは <see cref="LastError"/> に残す
    /// </returns>
    public async Task SyncAsync(CancellationToken cancellationToken = default)
        => await TrySyncAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>差分を 1 回読んで、読めたかを返す</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// DB から読めなかった (<see cref="DataFileException"/>)ときだけ false。
    /// 保存先が使えないとき、ログインの前は読むものが無いので true。
    /// </returns>
    private async Task<bool> TrySyncAsync(CancellationToken cancellationToken)
    {
        var read = true;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!repository.IsAvailable || !currentUser.IsIdentified)
            {
                return true;
            }

            await SyncCoreAsync(currentUser.Id, cancellationToken).ConfigureAwait(false);
            LastError = null;
        }
        catch (DataFileException ex)
        {
            LastError = ex.Message;
            read = false;
        }
        finally
        {
            _gate.Release();
        }

        Synced?.Invoke();
        return read;
    }

    /// <summary>会話のメッセージを新しい順に読む</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="beforeMessageId">
    /// この番号より小さいメッセージだけを読む
    /// 0 のときは最新から
    /// </param>
    /// <param name="count">読む最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// メッセージ
    /// 新しい順
    /// </returns>
    /// <exception cref="DataFileException">
    /// 読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<IReadOnlyList<ChatMessage>> LoadMessagesAsync(int conversationId, int beforeMessageId, int count, CancellationToken cancellationToken = default)
        => repository.GetMessagesAsync(conversationId, beforeMessageId, count, cancellationToken);

    /// <summary>端末の控えから会話のメッセージを読む</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 控えのメッセージ
    /// 古いものが先
    /// 無ければ空
    /// </returns>
    public Task<IReadOnlyList<ChatMessage>> ReadCachedMessagesAsync(int conversationId, CancellationToken cancellationToken = default)
        => _userId == 0 ? Task.FromResult<IReadOnlyList<ChatMessage>>([]) : cache.ReadMessagesAsync(_userId, conversationId, cancellationToken);

    /// <summary>端末の控えに会話のメッセージを書く</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="messages">
    /// いま見えているメッセージ
    /// 古いものが先
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書いたことを表すタスク</returns>
    public Task WriteCachedMessagesAsync(int conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        => _userId == 0 ? Task.CompletedTask : cache.WriteMessagesAsync(_userId, conversationId, messages, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        _subscription?.Dispose();
        _cancellation.Cancel();
        _cancellation.Dispose();
        // _gate は破棄しない。読んでいる最中の処理が、終わるときに解放するため。破棄すると、アプリの終了のたびに、解放が失敗して異常終了の画面が出る。プロセスの終了で消える。
    }

    /// <summary>画面の状態から読む間隔を決める</summary>
    /// <param name="inFront">会話を開いていて、ウィンドウが前面にあるか。</param>
    /// <returns>前面なら <see cref="FrontInterval"/>、それ以外は <see cref="BackgroundInterval"/>。</returns>
    private static TimeSpan IntervalFor(bool inFront) => inFront ? FrontInterval : BackgroundInterval;

    /// <summary>読んで、人の一覧と控えに取り込み、届いたメッセージを知らせる</summary>
    /// <param name="me">自分の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読んで取り込んだことを表すタスク</returns>
    /// <exception cref="DataFileException">読めなかった</exception>
    private async Task SyncCoreAsync(int me, CancellationToken cancellationToken)
    {
        if (_userId != me)
        {
            // ログインした人が替わった。前の人の控えは捨てて、最初から読む
            if (_userId != 0)
            {
                await cache.ClearAsync(cancellationToken).ConfigureAwait(false);
            }
            _userId = me;
            _nextBase = null;
            _sessionStart = null;
            _conversations = [];
            _notified.Clear();
            _reportedRead.Clear();
        }

        if (_nextBase is null && await cache.ReadListAsync(me, cancellationToken).ConfigureAwait(false) is { } cached)
        {
            // 最初に、控えから出す。続けて、差分を読む。古い形の控えは、読み込みの時点で捨てられている
            _nextBase = cached.NextBase;
            _conversations = cached.Conversations;
            Synced?.Invoke();
        }

        MessageSyncResult result;
        IReadOnlyList<ConversationSummary> previous = [];
        if (_nextBase is { } known)
        {
            result = await repository.GetChangesAsync(MessageSync.SinceOf(known), cancellationToken).ConfigureAwait(false);
            previous = _conversations;
            _conversations = Merge(_conversations, result.Conversations);
        }
        else
        {
            result = await repository.GetStartAsync(cancellationToken).ConfigureAwait(false);
            _conversations = result.Conversations;
        }

        // 抜けた会話は、一覧と控えから消す。入り直したときに、古い控えと読み直した分の間に、抜けが出ないようにする
        foreach (var leftId in result.LeftConversationIds)
        {
            _conversations = [.. _conversations.Where(conversation => conversation.Id != leftId)];
            await cache.WriteMessagesAsync(me, leftId, [], cancellationToken).ConfigureAwait(false);
        }

        _nextBase = result.NextBase;
        _sessionStart ??= result.NextBase;
        await cache.WriteListAsync(CachedConversationList.Create(me, result.NextBase, _conversations), cancellationToken).ConfigureAwait(false);

        foreach (var leftId in result.LeftConversationIds)
        {
            ConversationLeft?.Invoke(leftId);
        }

        await MergeIntoMessageCachesAsync(me, result.Messages, previous, cancellationToken).ConfigureAwait(false);

        if (result.Messages.Count > 0)
        {
            MessagesArrived?.Invoke(result.Messages);

            // 起動してから届いた、他の人のメッセージだけを、新着として知らせる。閉じている間に届いた分は、出さない
            var start = _sessionStart.Value;
            var fresh = result.Messages.Where(message => message.SenderUserId != me && !message.IsSystem && message.CreatedAt >= start && _notified.Add(message.Id)).ToList();
            if (fresh.Count > 0)
            {
                NewMessagesFromOthers?.Invoke(fresh);
            }
        }
    }

    /// <summary>差分で届いたメッセージを会話の控えへ足す</summary>
    /// <param name="me">自分の番号</param>
    /// <param name="messages">差分で届いた、または更新されたメッセージ。</param>
    /// <param name="previous">今回の差分を取り込む前の、会話の要約。</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書いたことを表すタスク</returns>
    /// <remarks>
    /// 会話を開く前に控えを最新にしておくため。
    /// 差分は前回の基準より後の変更を、すべて返す。
    /// 足すのは控えの最後のメッセージが前回の要約の最後のメッセージと同じ会話だけ。
    /// 前回の時点で、控えが最新だった証拠がこれしかないため。
    /// 違う会話 (控えの無い会話・前回以降に取りこぼした会話)は足すと途中が抜けるので書かず、開いたときに DB から読み直す。
    /// 控えの先頭より古い番号のメッセージは足さない。
    /// 控えは新しいほうから切れ目なく持つため。
    /// 開いている会話は画面が届いたメッセージを持つので、ここでは書かない。
    /// 同じメッセージが何度届いても、番号と更新日時で突き合わせるので、結果は変わらない。
    /// </remarks>
    private async Task MergeIntoMessageCachesAsync(
        int me, IReadOnlyList<ChatMessage> messages, IReadOnlyList<ConversationSummary> previous, CancellationToken cancellationToken)
    {
        foreach (var group in messages.GroupBy(message => message.ConversationId))
        {
            if (group.Key == _openConversationId || previous.FirstOrDefault(conversation => conversation.Id == group.Key) is not { } before)
            {
                continue;
            }

            var cached = await cache.ReadMessagesAsync(me, group.Key, cancellationToken).ConfigureAwait(false);
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
            await cache.WriteMessagesAsync(me, group.Key, [.. byId.Values.OrderBy(message => message.Id)], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>会話の要約を番号で突き合わせて取り込み、最後のメッセージが新しい順に並べる</summary>
    /// <param name="current">いまの要約</param>
    /// <param name="changed">変わった要約</param>
    /// <returns>取り込んだあとの要約</returns>
    private static IReadOnlyList<ConversationSummary> Merge(IReadOnlyList<ConversationSummary> current, IReadOnlyList<ConversationSummary> changed)
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
}
