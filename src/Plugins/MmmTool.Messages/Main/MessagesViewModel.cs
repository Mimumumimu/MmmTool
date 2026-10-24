using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>
/// メッセージのページの ViewModel。左に人の一覧と探す欄、右に会話と入力欄を持つ。
/// </summary>
/// <param name="repository">会話とメッセージの保存先</param>
/// <param name="sync">会話とメッセージを読み続ける部品</param>
/// <remarks>
/// 画面は、左の会話の一覧から会話を開く。新しい相手には、「新しい会話」で宛先に選べる人の一覧を出して選ぶ。やり取りが無い人を選ぶと、空の会話の画面を出す。会話は、最初の送信で作る。
/// 最初は、端末の控えから出し、続けて DB から最新を読む。新着は、読み続ける部品 (<see cref="MessageSyncService"/>)から受ける。
/// 外部へ送る処理は、MmmTool には無い。「送信」は、メッセージと通知の状態の行を DB に書くだけで、実際に送るのは MmmBatch。
/// </remarks>
public sealed partial class MessagesViewModel(IMessageRepository repository, MessageSyncService sync) : ObservableObject, IDisposable
{
    /// <summary>宛先に選べる人を、1 回に読む最大の件数</summary>
    private const int SearchCount = 100;

    /// <summary>画面の変更を、UI スレッドで行うための入れ物。画面が、UI スレッドのものに替える</summary>
    public Action<Action> Post { get; set; } = action => action();

    /// <summary>一番下までスクロールしてほしい。引数が true のときは、見ている位置が一番下の近くのときだけ</summary>
    /// <remarks>会話を開いたときは、いつも一番下へ。新着が届いたときは、さかのぼって読んでいる人の位置を動かさない。</remarks>
    public event Action<bool>? ScrollToEndRequested;

    /// <summary>通知を押して開いたメッセージの位置までスクロールしてほしい。引数はメッセージの番号</summary>
    public event Action<int>? ScrollToMessageRequested;

    /// <summary>やり取りのある会話。最後のメッセージが新しい順</summary>
    public ObservableCollection<ConversationItem> Conversations { get; } = [];

    /// <summary>宛先に選べる人。人を選ぶ画面のときだけ出す。表示名の順</summary>
    public ObservableCollection<PersonItem> People { get; } = [];

    /// <summary>開いている会話のメッセージ。古いものが先</summary>
    public ObservableCollection<ChatMessageItem> Messages { get; } = [];

    /// <summary>読み込み・送信のエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>DB モード (サーバーモード)か。false のときは使えない</summary>
    public bool IsAvailable { get; } = repository.IsAvailable;

    /// <summary>DB モードでないことを知らせるか</summary>
    public bool IsUnavailableVisible => !IsAvailable;

    /// <summary>宛先の人を選ぶ画面にしているか。「新しい会話」で入り、人を選ぶか「戻る」で出る</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConversationListVisible), nameof(IsPeopleListVisible), nameof(EmptyNoticeText), nameof(IsEmptyNoticeVisible))]
    public partial bool IsPickingPerson { get; set; }

    /// <summary>会話の一覧を出すか。人を選ぶ画面でないとき</summary>
    public bool IsConversationListVisible => !IsPickingPerson;

    /// <summary>人の一覧を出すか。人を選ぶ画面のとき</summary>
    public bool IsPeopleListVisible => IsPickingPerson;

    /// <summary>会話が 1 つも無いか。読み込みが済んで、1 件も無いとき</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyNoticeText), nameof(IsEmptyNoticeVisible))]
    public partial bool IsConversationsEmpty { get; set; }

    /// <summary>宛先に選べる人が 1 人もいないか。読み込みが済んで、1 件も無いとき</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyNoticeText), nameof(IsEmptyNoticeVisible))]
    public partial bool IsPeopleEmpty { get; set; }

    /// <summary>一覧が空のときの案内。空でないときは空文字</summary>
    public string EmptyNoticeText => IsPickingPerson
        ? (IsPeopleEmpty ? "宛先にできる人がいません。" : "")
        : (IsConversationsEmpty ? "会話はまだありません。「新しい会話」から宛先を選んでください。" : "");

    /// <summary>一覧が空のときの案内を出すか</summary>
    public bool IsEmptyNoticeVisible => EmptyNoticeText.Length > 0;

    /// <summary>会話を開いているか。開いていないときは、右に案内を出す</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConversationClosed))]
    public partial bool IsConversationOpen { get; set; }

    /// <summary>会話を開いていないか</summary>
    public bool IsConversationClosed => !IsConversationOpen;

    /// <summary>開いている会話の名前。1 対 1 は相手の表示名</summary>
    [ObservableProperty]
    public partial string ConversationTitle { get; set; } = "";

    /// <summary>入力欄が押せるか。相手が受け取り先を指定しているときだけ</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(IsNoReceiveTargetVisible))]
    public partial bool PeerCanReceive { get; set; } = true;

    /// <summary>「受け取り先が未設定です」の案内を出すか</summary>
    public bool IsNoReceiveTargetVisible => IsConversationOpen && !PeerCanReceive;

    /// <summary>入力した本文</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Body { get; set; } = "";

    /// <summary>返信の元にするメッセージ。返信でなければ null</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReplyTo))]
    [NotifyPropertyChangedFor(nameof(ReplyToText))]
    public partial ChatMessageItem? ReplyTo { get; set; }

    /// <summary>返信の元を出すか</summary>
    public bool HasReplyTo => ReplyTo is not null;

    /// <summary>返信の元の、送った人の名前と本文の 1 行目</summary>
    public string ReplyToText => ReplyTo is { } reply ? $"{(reply.IsMine ? "自分" : reply.SenderText)}  {FirstLine(reply.Body)}" : "";

    /// <summary>送信中か。送信中は、続けて押せない</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    /// <summary>最新でない可能性を知らせるか。読み込みに失敗して、端末の控えだけを出しているとき</summary>
    [ObservableProperty]
    public partial bool IsStale { get; set; }

    /// <summary>開いている会話の番号。まだ会話が無い (空の会話の画面)ときと、開いていないときは 0</summary>
    private int _conversationId;

    /// <summary>空の会話の画面の、1 対 1 の相手の番号。会話があるときは 0</summary>
    private int _draftPeerUserId;

    /// <summary>これより古いメッセージが、まだあるか</summary>
    private bool _hasMore;

    /// <summary>古いメッセージを読み足している最中か</summary>
    private bool _loadingOlder;

    /// <summary>ウィンドウが前面にあり、ページが見えているか</summary>
    private bool _visible;

    /// <summary>人を選ぶ画面に入るたびに進む番号。古い読み込みの結果を、使わないために使う</summary>
    private int _pickVersion;

    /// <summary>会話を開くたびに進む番号。古い会話の読み込みの結果を、使わないために使う</summary>
    private int _openVersion;

    /// <summary>通知を押して開くとき、位置を合わせるメッセージの番号。無ければ 0</summary>
    private int _highlightId;

    /// <summary>読み込みの失敗として、いま出しているエラーの文。読み込みの成功で、これだけを消す。出していなければ null</summary>
    private string? _syncErrorText;

    /// <summary>ページを開いたときに呼ぶ。読み込みの通知を受け始め、会話の一覧を読む</summary>
    /// <returns>始めたことを表すタスク</returns>
    public async Task InitializeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        sync.Synced -= OnSynced;
        sync.Synced += OnSynced;
        sync.MessagesArrived -= OnMessagesArrived;
        sync.MessagesArrived += OnMessagesArrived;
        RefreshConversations();
        await sync.SyncAsync().ConfigureAwait(true);
    }

    /// <summary>ページが見える・見えないの変化を伝える。読む間隔を決めるために使う</summary>
    /// <param name="visible">ウィンドウが前面にあり、ページが見えているか</param>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        sync.SetViewState(IsConversationOpen ? _conversationId : 0, visible);
    }

    /// <summary>通知ダイアログのクリックなどから、会話を開く</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="messageId">位置を合わせるメッセージの番号。無ければ 0</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    public async Task OpenConversationAsync(int conversationId, int messageId = 0)
    {
        _highlightId = messageId;
        var summary = sync.Conversations.FirstOrDefault(conversation => conversation.Id == conversationId);
        await OpenAsync(conversationId, 0, summary?.Name ?? "メッセージ", summary?.PeerCanReceive ?? true).ConfigureAwait(true);
    }

    /// <summary>人の一覧から、会話を開く</summary>
    /// <param name="item">選んだ行</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    public Task OpenAsync(ConversationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _highlightId = 0;
        return OpenAsync(item.Id, 0, item.Name, item.Source.PeerCanReceive);
    }

    /// <summary>選んだ人との会話を開く。まだ会話が無いときは、空の会話の画面を出す。開けたら、会話の一覧へ戻る</summary>
    /// <param name="person">選んだ人</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    [RelayCommand]
    private async Task OpenPersonAsync(PersonItem person)
    {
        try
        {
            var conversationId = await repository.FindDirectConversationAsync(person.Source.UserId);
            _highlightId = 0;
            await OpenAsync(conversationId, person.Source.UserId, person.DisplayName, peerCanReceive: true);
            CancelPickPerson();
        }
        catch (DataFileException ex)
        {
            Error.Show($"会話を開けませんでした: {ex.Message}");
        }
    }

    /// <summary>返信を始める</summary>
    /// <param name="message">返信の元のメッセージ</param>
    [RelayCommand]
    private void StartReply(ChatMessageItem message) => ReplyTo = message;

    /// <summary>返信をやめる</summary>
    [RelayCommand]
    private void CancelReply() => ReplyTo = null;

    /// <summary>古いメッセージを、50 件読み足す。画面が、先頭に近づいたときに呼ぶ</summary>
    /// <returns>読み足しの完了を表すタスク</returns>
    public async Task LoadOlderAsync()
    {
        if (!_hasMore || _loadingOlder || _conversationId == 0 || Messages.Count == 0)
        {
            return;
        }

        _loadingOlder = true;
        try
        {
            var older = await sync.LoadMessagesAsync(_conversationId, Messages[0].Id, MessageSync.PageSize + 1);
            _hasMore = older.Count > MessageSync.PageSize;
            foreach (var message in older.Take(MessageSync.PageSize))
            {
                Messages.Insert(0, ToItem(message));
            }
        }
        catch (DataFileException ex)
        {
            Error.Show($"古いメッセージを読み込めませんでした: {ex.Message}");
        }
        finally
        {
            _loadingOlder = false;
        }
    }

    /// <summary>読み込みの通知を受けるのをやめる。ページが外れたときに呼ぶ。次に開くと、また受け始める</summary>
    public void Detach()
    {
        sync.Synced -= OnSynced;
        sync.MessagesArrived -= OnMessagesArrived;
    }

    /// <inheritdoc />
    public void Dispose() => Detach();

    /// <summary>人を選ぶ画面に入る。宛先に選べる人を、開くたびに読み直す</summary>
    /// <returns>読む処理の完了を表すタスク</returns>
    [RelayCommand]
    private async Task PickPersonAsync()
    {
        var version = ++_pickVersion;
        People.Clear();
        IsPeopleEmpty = false;
        IsPickingPerson = true;

        try
        {
            var found = await repository.SearchPeopleAsync("", SearchCount);
            if (version != _pickVersion)
            {
                return;
            }

            foreach (var person in found)
            {
                People.Add(new PersonItem(person));
            }
            IsPeopleEmpty = People.Count == 0;
        }
        catch (DataFileException ex)
        {
            Error.Show($"宛先の人を読み込めませんでした: {ex.Message}");
        }
    }

    /// <summary>人を選ぶ画面から、会話の一覧へ戻る</summary>
    [RelayCommand]
    private void CancelPickPerson()
    {
        _pickVersion++;
        IsPickingPerson = false;
        People.Clear();
    }

    /// <summary>会話を開く。控えから出して、続けて DB から最新を読む</summary>
    /// <param name="conversationId">会話の番号。まだ会話が無いときは 0</param>
    /// <param name="peerUserId">会話の番号が 0 のとき、1 対 1 の相手の番号</param>
    /// <param name="title">会話の名前</param>
    /// <param name="peerCanReceive">相手が受け取り先を指定しているか</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    private async Task OpenAsync(int conversationId, int peerUserId, string title, bool peerCanReceive)
    {
        var version = ++_openVersion;
        _conversationId = conversationId;
        _draftPeerUserId = conversationId == 0 ? peerUserId : 0;
        ConversationTitle = title;
        PeerCanReceive = peerCanReceive;
        IsConversationOpen = true;
        IsStale = false;
        Body = "";
        ReplyTo = null;
        Messages.Clear();
        _hasMore = false;
        Error.Clear();
        sync.SetViewState(conversationId, _visible);
        if (conversationId == 0)
        {
            return;
        }

        // まず控えから出す
        var cached = await sync.ReadCachedMessagesAsync(conversationId);
        if (version != _openVersion)
        {
            return;
        }
        foreach (var message in cached)
        {
            Messages.Add(ToItem(message));
        }
        ScrollToEndRequested?.Invoke(false);

        try
        {
            var newest = await sync.LoadMessagesAsync(conversationId, 0, MessageSync.PageSize + 1);
            if (version != _openVersion)
            {
                return;
            }

            MergeNewest(newest.Take(MessageSync.PageSize).Reverse().ToList(), newest.Count > MessageSync.PageSize);
            await sync.WriteCachedMessagesAsync(conversationId, [.. Messages.Select(item => item.Source)]);
        }
        catch (DataFileException ex)
        {
            // 読めないときは、控えを、読むだけで出す
            IsStale = Messages.Count > 0;
            Error.Show($"会話を読み込めませんでした: {ex.Message}");
            return;
        }

        // 通知を押して開いたときは、そのメッセージの位置まで、古いメッセージを読み足す
        while (_highlightId > 0 && _hasMore && Messages.Count > 0 && _highlightId < Messages[0].Id && version == _openVersion)
        {
            var before = Messages.Count;
            await LoadOlderAsync();
            if (Messages.Count == before)
            {
                break;
            }
        }

        if (_highlightId > 0)
        {
            ScrollToMessageRequested?.Invoke(_highlightId);
        }
        else
        {
            ScrollToEndRequested?.Invoke(false);
        }
    }

    /// <summary>DB から読んだ最新の 1 ページを、控えと合わせる。控えは、新しいほうから切れ目なく持つ</summary>
    /// <param name="fetched">読んだメッセージ。古いものが先</param>
    /// <param name="pageHasMore">読んだページより古いメッセージが、まだあるか</param>
    private void MergeNewest(IReadOnlyList<ChatMessage> fetched, bool pageHasMore)
    {
        if (Messages.Count > 0 && fetched.Count > 0 && fetched[0].Id <= Messages[^1].Id)
        {
            // 控えと重なる。番号で突き合わせて、取り込む
            var byId = Messages.ToDictionary(item => item.Id, item => item.Source);
            foreach (var message in fetched)
            {
                byId[message.Id] = message;
            }
            Messages.Clear();
            foreach (var message in byId.Values.OrderBy(message => message.Id))
            {
                Messages.Add(ToItem(message));
            }
            _hasMore = pageHasMore || Messages.Count > fetched.Count;
            return;
        }

        // 控えと重ならない。間が抜けるので、控えを捨てて、読んだページだけにする
        Messages.Clear();
        foreach (var message in fetched)
        {
            Messages.Add(ToItem(message));
        }
        _hasMore = pageHasMore;
    }

    /// <summary>読み込みを 1 回終えたとき、人の一覧とエラーを画面に合わせる</summary>
    private void OnSynced() => Post(() =>
    {
        RefreshConversations();

        // 読み込みの失敗だけを出し入れする。送れなかった・探せなかった、などのエラーを、読み込みの成功で消さない
        var text = sync.LastError is { } error ? $"読み込めませんでした: {error}" : null;
        if (text is not null)
        {
            Error.Show(text);
            _syncErrorText = text;
        }
        else if (_syncErrorText is not null)
        {
            if (Error.Message == _syncErrorText)
            {
                Error.Clear();
            }
            _syncErrorText = null;
        }
    });

    /// <summary>メッセージが届いた、または更新されたとき、開いている会話の分を取り込む</summary>
    /// <param name="arrived">届いた、または更新されたメッセージ</param>
    private void OnMessagesArrived(IReadOnlyList<ChatMessage> arrived) => Post(() =>
    {
        if (_conversationId == 0)
        {
            return;
        }

        var mine = arrived.Where(message => message.ConversationId == _conversationId).ToList();
        if (mine.Count == 0)
        {
            return;
        }

        var added = false;
        foreach (var message in mine.OrderBy(message => message.Id))
        {
            var index = Messages.ToList().FindIndex(item => item.Id == message.Id);
            if (index >= 0)
            {
                if (message.UpdatedAt >= Messages[index].Source.UpdatedAt)
                {
                    Messages[index] = ToItem(message);
                }
            }
            else
            {
                Messages.Add(ToItem(message));
                added = true;
            }
        }

        if (added)
        {
            ScrollToEndRequested?.Invoke(true);
        }
        sync.WriteCachedMessagesAsync(_conversationId, [.. Messages.Select(item => item.Source)]).Forget();
    });

    /// <summary>人の一覧を、読み続ける部品が持つ要約に合わせる。変わった行だけ差し替える</summary>
    private void RefreshConversations()
    {
        var summaries = sync.Conversations;
        for (var i = 0; i < summaries.Count; i++)
        {
            if (i >= Conversations.Count)
            {
                Conversations.Add(new ConversationItem(summaries[i]));
            }
            else if (!Conversations[i].Source.Equals(summaries[i]))
            {
                Conversations[i] = new ConversationItem(summaries[i]);
            }
        }
        while (Conversations.Count > summaries.Count)
        {
            Conversations.RemoveAt(Conversations.Count - 1);
        }

        IsConversationsEmpty = Conversations.Count == 0;
        if (IsConversationOpen && _conversationId > 0 && summaries.FirstOrDefault(conversation => conversation.Id == _conversationId) is { } open)
        {
            PeerCanReceive = open.PeerCanReceive;
        }
    }

    /// <summary>送れる状態か。相手が受け取り先を指定していて、本文が空白だけでない</summary>
    /// <returns>送れるとき true</returns>
    private bool CanSend() => IsConversationOpen && !IsSending && MessageDraft.CanSend(PeerCanReceive, Body);

    /// <summary>メッセージを送る。メッセージと通知の状態の行を DB に書く</summary>
    /// <returns>送る処理の完了を表すタスク</returns>
    /// <remarks>続けられる失敗なので、失敗は画面に出す。送れたら、本文と返信を空に戻す。</remarks>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        IsSending = true;
        try
        {
            var result = await repository.SendAsync(_conversationId, _conversationId > 0 ? 0 : _draftPeerUserId, Body, ReplyTo?.Id ?? 0);
            if (result.Outcome != SendMessageOutcome.Sent)
            {
                Error.Show($"送れませんでした: {OutcomeText(result.Outcome)}");
                if (result.Outcome == SendMessageOutcome.NoReceiveTarget)
                {
                    PeerCanReceive = false;
                }
                return;
            }

            Error.Clear();
            Body = "";
            ReplyTo = null;
            if (_conversationId == 0)
            {
                // 会話ができた。以降は、この会話として読む
                _conversationId = result.ConversationId;
                _draftPeerUserId = 0;
                sync.SetViewState(_conversationId, _visible);
            }
            await sync.SyncAsync();
        }
        catch (DataFileException ex)
        {
            Error.Show($"送れませんでした: {ex.Message}");
        }
        finally
        {
            IsSending = false;
        }
    }

    /// <summary>送れなかった理由を、画面に出す文にする</summary>
    /// <param name="outcome">結果の区分</param>
    /// <returns>理由の文</returns>
    private static string OutcomeText(SendMessageOutcome outcome) => outcome switch
    {
        SendMessageOutcome.NoReceiveTarget => MessageDraft.NoReceiveTargetMessage,
        SendMessageOutcome.ReplyInvalid => "返信元のメッセージが見つかりません。",
        _ => "会話が見つかりません。",
    };

    /// <summary>メッセージを、画面の行にする</summary>
    /// <param name="message">メッセージ</param>
    /// <returns>画面の行。自分が送ったメッセージかは、読み込んだ人の番号で決める</returns>
    private ChatMessageItem ToItem(ChatMessage message) => new(message, message.SenderUserId == sync.UserId);

    /// <summary>本文の 1 行目を取る</summary>
    /// <param name="text">本文</param>
    /// <returns>最初の改行より前の文字</returns>
    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? text : text[..end];
    }
}
