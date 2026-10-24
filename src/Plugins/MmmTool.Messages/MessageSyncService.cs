using MmmSdk.Core.Components.Storage;
using MmmTool.Messages.Cache;
using MmmTool.Messages.Core;
using MmmTool.Users.Core;

namespace MmmTool.Messages;

/// <summary>
/// 会話とメッセージを、DB から読み続ける。人の一覧と、開いている会話の新着を、画面と通知ダイアログに渡す。
/// </summary>
/// <remarks>
/// サーバーを通さず、PC が DB を、場面ごとの間隔で読む。間隔はどれも仮。
/// 会話を開いていて、ウィンドウが前面にあるときは 2 秒、それ以外でアプリが動いている間は 10 秒。
/// 読むのは、どの場面も同じ差分の取得で、会話の数に関係なく、索引 3 回。人の一覧は、読んだ結果から作り直す。
/// 起動直後は、まず端末の控え (<see cref="MessageCacheStore"/>)から出し、続けて差分を読む。控えは、いつでも作り直せる。
/// 読んだ結果は、任意のスレッドから通知する。画面は、UI スレッドに切り替えて使う。
/// </remarks>
/// <param name="repository">会話とメッセージの保存先</param>
/// <param name="currentUser">今のユーザー</param>
/// <param name="cache">端末の控え</param>
public sealed class MessageSyncService(IMessageRepository repository, CurrentUser currentUser, MessageCacheStore cache) : IDisposable
{
    /// <summary>会話を開いていて、ウィンドウが前面にあるときの間隔。仮</summary>
    private static readonly TimeSpan FrontInterval = TimeSpan.FromSeconds(2);

    /// <summary>それ以外で、アプリが動いている間の間隔。仮</summary>
    private static readonly TimeSpan BackgroundInterval = TimeSpan.FromSeconds(10);

    /// <summary>読む処理を、同時に 2 つ走らせないための鍵</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>待ち時間の途中で、すぐ読みたいとき、ループを起こす</summary>
    private readonly SemaphoreSlim _wake = new(0);

    /// <summary>通知ダイアログに出した、他の人のメッセージの番号。同じメッセージを、続けて出さない</summary>
    private readonly HashSet<int> _notified = [];

    /// <summary>ループを止めるための取り消し</summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>いま開いている会話の番号。開いていなければ 0</summary>
    private int _openConversationId;

    /// <summary>ウィンドウが前面にあるか</summary>
    private bool _inFront;

    /// <summary>読んだ人の番号。まだ読んでいなければ 0</summary>
    private int _userId;

    /// <summary>次の差分の取得の基準にする日時。まだ読んでいなければ null</summary>
    private DateTimeOffset? _nextBase;

    /// <summary>起動して最初に読んだ日時。DB の時計。これより前に送られたメッセージは、通知ダイアログに出さない</summary>
    private DateTimeOffset? _sessionStart;

    /// <summary>会話の要約。最後のメッセージが新しい順</summary>
    private IReadOnlyList<ConversationSummary> _conversations = [];

    /// <summary>会話の要約。最後のメッセージが新しい順</summary>
    public IReadOnlyList<ConversationSummary> Conversations => _conversations;

    /// <summary>読んだ人の番号。まだ読んでいなければ 0</summary>
    public int UserId => _userId;

    /// <summary>最後の読み込みの失敗の理由。成功していれば null</summary>
    public string? LastError { get; private set; }

    /// <summary>読み込みを 1 回終えた。成功でも失敗でも、発火する。任意のスレッドから発火する</summary>
    public event Action? Synced;

    /// <summary>メッセージが届いた、または更新された。任意のスレッドから発火する</summary>
    public event Action<IReadOnlyList<ChatMessage>>? MessagesArrived;

    /// <summary>他の人から届いた新着。起動してから届いたもので、まだ通知していないものだけ。任意のスレッドから発火する</summary>
    public event Action<IReadOnlyList<ChatMessage>>? NewMessagesFromOthers;

    /// <summary>読み続けるループを始める</summary>
    /// <param name="isEnabled">機能がオンか。オフの間は読まない</param>
    /// <remarks>アプリの起動時に 1 回だけ呼ぶ。</remarks>
    public void Start(Func<bool> isEnabled)
    {
        ArgumentNullException.ThrowIfNull(isEnabled);

        if (_cancellation is not null)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        _ = Task.Run(() => RunLoopAsync(isEnabled, token), token);
    }

    /// <summary>画面の状態を伝える。間隔を決めるために使う</summary>
    /// <param name="openConversationId">いま開いている会話の番号。開いていなければ 0</param>
    /// <param name="inFront">ウィンドウが前面にあり、メッセージのページが見えているか</param>
    /// <remarks>会話を開いて前面になったときは、待たずに、すぐ読む。</remarks>
    public void SetViewState(int openConversationId, bool inFront)
    {
        var becameActive = inFront && openConversationId > 0 && !(_inFront && _openConversationId == openConversationId);
        _openConversationId = openConversationId;
        _inFront = inFront;
        if (becameActive)
        {
            _wake.Release();
        }
    }

    /// <summary>いま開いている会話の番号。通知ダイアログに、前面で見ている会話の新着を出さないために使う</summary>
    /// <returns>前面で見ている会話の番号。前面でなければ 0</returns>
    public int GetFrontConversationId() => _inFront ? _openConversationId : 0;

    /// <summary>差分を 1 回読む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読む処理の完了を表すタスク。読めなかったときは <see cref="LastError"/> に残す</returns>
    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!repository.IsAvailable || !currentUser.IsIdentified)
            {
                return;
            }

            await SyncCoreAsync(currentUser.Id, cancellationToken).ConfigureAwait(false);
            LastError = null;
        }
        catch (DataFileException ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            _gate.Release();
        }

        Synced?.Invoke();
    }

    /// <summary>会話のメッセージを、新しい順に読む</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="beforeMessageId">この番号より小さいメッセージだけを読む。0 のときは最新から</param>
    /// <param name="count">読む最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>メッセージ。新しい順</returns>
    /// <exception cref="DataFileException">読めなかった。メッセージは画面に出せる。</exception>
    public Task<IReadOnlyList<ChatMessage>> LoadMessagesAsync(int conversationId, int beforeMessageId, int count, CancellationToken cancellationToken = default)
        => repository.GetMessagesAsync(conversationId, beforeMessageId, count, cancellationToken);

    /// <summary>端末の控えから、会話のメッセージを読む</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>控えのメッセージ。古いものが先。無ければ空</returns>
    public Task<IReadOnlyList<ChatMessage>> ReadCachedMessagesAsync(int conversationId, CancellationToken cancellationToken = default)
        => _userId == 0 ? Task.FromResult<IReadOnlyList<ChatMessage>>([]) : cache.ReadMessagesAsync(_userId, conversationId, cancellationToken);

    /// <summary>端末の控えに、会話のメッセージを書く</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="messages">いま見えているメッセージ。古いものが先</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書いたことを表すタスク</returns>
    public Task WriteCachedMessagesAsync(int conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        => _userId == 0 ? Task.CompletedTask : cache.WriteMessagesAsync(_userId, conversationId, messages, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _gate.Dispose();
        _wake.Dispose();
    }

    /// <summary>読み続けるループ</summary>
    /// <param name="isEnabled">機能がオンか</param>
    /// <param name="cancellationToken">止めるためのトークン</param>
    /// <returns>ループの完了を表すタスク</returns>
    private async Task RunLoopAsync(Func<bool> isEnabled, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var interval = _inFront && _openConversationId > 0 ? FrontInterval : BackgroundInterval;
            try
            {
                await _wake.WaitAsync(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!isEnabled())
            {
                continue;
            }

            try
            {
                await SyncAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // 予想外の失敗でも、ループを止めない。止めると、新着が静かに届かなくなる。画面の読み込みのエラーに出して、次の回にもう一度読む
                LastError = $"予想外のエラー: {ex.Message}";
                Synced?.Invoke();
            }
        }
    }

    /// <summary>読んで、人の一覧と控えに取り込み、届いたメッセージを知らせる</summary>
    /// <param name="me">自分の番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読んで取り込んだことを表すタスク</returns>
    /// <exception cref="DataFileException">読めなかった。</exception>
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
        }

        if (_nextBase is null && await cache.ReadListAsync(me, cancellationToken).ConfigureAwait(false) is { } cached)
        {
            // 最初に、控えから出す。続けて、差分を読む
            _nextBase = cached.NextBase;
            _conversations = cached.Conversations;
            Synced?.Invoke();
        }

        MessageSyncResult result;
        if (_nextBase is { } known)
        {
            result = await repository.GetChangesAsync(MessageSync.SinceOf(known), cancellationToken).ConfigureAwait(false);
            _conversations = Merge(_conversations, result.Conversations);
        }
        else
        {
            result = await repository.GetStartAsync(cancellationToken).ConfigureAwait(false);
            _conversations = result.Conversations;
        }

        _nextBase = result.NextBase;
        _sessionStart ??= result.NextBase;
        await cache.WriteListAsync(new CachedConversationList(me, result.NextBase, _conversations), cancellationToken).ConfigureAwait(false);

        if (result.Messages.Count > 0)
        {
            MessagesArrived?.Invoke(result.Messages);

            // 起動してから届いた、他の人のメッセージだけを、新着として知らせる。閉じている間に届いた分は、出さない
            var start = _sessionStart.Value;
            var fresh = result.Messages.Where(message => message.SenderUserId != me && message.CreatedAt >= start && _notified.Add(message.Id)).ToList();
            if (fresh.Count > 0)
            {
                NewMessagesFromOthers?.Invoke(fresh);
            }
        }
    }

    /// <summary>会話の要約を、番号で突き合わせて取り込み、最後のメッセージが新しい順に並べる</summary>
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
