using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Paths;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>メッセージのページの ViewModel</summary>
/// <param name="repository">会話とメッセージの保存先</param>
/// <param name="sync">会話とメッセージを読み続ける部品</param>
/// <param name="state">画面で選んだ状態 (ページを作り直しても引き継ぐ)</param>
/// <param name="opener">本文のリンク先を開く処理</param>
/// <param name="dialogs">
/// 確認ダイアログを開く口
/// グループを抜けるときの確認に使う
/// </param>
/// <remarks>
/// 左に人の一覧と探す欄、右に会話と入力欄を持つ。
/// 画面は左の会話の一覧から会話を開く。
/// 新しい相手には「新しい会話」で宛先に選べる人の一覧を出して選ぶ。
/// やり取りが無い人を選ぶと空の会話の画面を出す。
/// 会話は最初の送信で作る。
/// 新しいグループは宛先の一覧の先頭の「新しいグループ」から、名前と参加者を選んで、空のグループの画面を出す。
/// 会話は 1 対 1 と同じく、最初の送信で作る。
/// 最初は端末の控えから出し、続けて DB から最新を読む。
/// 新着は読み続ける部品 (<see cref="MessageSyncService"/>)から受ける。
/// 外部へ送る処理は MmmTool には無い。
/// 「送信」はメッセージと通知の状態の行を DB に書くだけで、実際に送るのは MmmBatch。
/// </remarks>
public sealed partial class MessagesViewModel(
    IMessageRepository repository, MessageSyncService sync, MessagesViewState state, IPathOpener opener, IDialogService dialogs) : ObservableObject, IDisposable
{
    /// <summary>宛先に選べる人を 1 回に読む最大の件数</summary>
    private const int SearchCount = 100;

    /// <summary>画面の変更を UI スレッドで行うための入れ物</summary>
    /// <remarks>画面が UI スレッドのものに替える</remarks>
    public Action<Action> Post { get; set; } = action => action();

    /// <summary>一番下までスクロールしてほしい</summary>
    /// <remarks>
    /// 引数が true のときは見ている位置が一番下の近くのときだけ
    /// 会話を開いたときはいつも一番下へ
    /// 新着が届いたときはさかのぼって読んでいる人の位置を動かさない
    /// </remarks>
    public event Action<bool>? ScrollToEndRequested;

    /// <summary>通知を押して開いたメッセージの位置までスクロールしてほしい</summary>
    /// <remarks>引数はメッセージの番号</remarks>
    public event Action<int>? ScrollToMessageRequested;

    /// <summary>会話の切り替えの準備をするタスクを返す入れ物</summary>
    /// <remarks>
    /// 画面が前の会話の見た目を撮って重ねるものに替える
    /// 会話を切り替え始めたときに呼ぶ
    /// メッセージの差し替えはこのタスクの完了を待ってから行う
    /// 撮る前に差し替えると前の会話が写らないため
    /// </remarks>
    public Func<Task> PrepareSwitchAsync { get; set; } = () => Task.CompletedTask;

    /// <summary>メッセージ欄の中身を丸ごと差し替えた直後</summary>
    /// <remarks>画面が一番下へ合わせてから見えるようにするために受ける</remarks>
    public event Action? MessagesReplaced;

    /// <summary>やり取りのある会話</summary>
    /// <remarks>最後のメッセージが新しい順</remarks>
    public ObservableCollection<ConversationItem> Conversations { get; } = [];

    /// <summary>開いている会話の一覧の行</summary>
    /// <remarks>
    /// 一覧の選択の印に使う
    /// 会話を開いていないときとまだ会話が無い空の会話の画面のときは null
    /// </remarks>
    public ConversationItem? SelectedConversation
        => IsConversationOpen && _conversationId > 0 ? Conversations.FirstOrDefault(item => item.Id == _conversationId) : null;

    /// <summary>宛先に選べる人</summary>
    /// <remarks>
    /// 人を選ぶ画面のときだけ出す
    /// 表示名の順
    /// </remarks>
    public ObservableCollection<PersonItem> People { get; } = [];

    /// <summary>開いている会話のメッセージ</summary>
    /// <remarks>古いものが先</remarks>
    public ObservableCollection<ChatMessageItem> Messages { get; } = [];

    /// <summary>読み込み・送信のエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>DB モードか</summary>
    /// <remarks>false のときは使えない</remarks>
    public bool IsAvailable { get; } = repository.IsAvailable;

    /// <summary>DB モードでないことを知らせるか</summary>
    public bool IsUnavailableVisible => !IsAvailable;

    /// <summary>宛先の人を選ぶ画面にしているか</summary>
    /// <remarks>「新しい会話」で入り、人を選ぶか「戻る」で出る。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConversationListVisible), nameof(IsPeopleListVisible), nameof(IsBackVisible), nameof(EmptyNoticeText), nameof(IsEmptyNoticeVisible))]
    public partial bool IsPickingPerson { get; set; }

    /// <summary>複数の人を選ぶ画面にしているか</summary>
    /// <remarks>「新しいグループ」と「人を追加」で入り、「戻る」か確定で出る。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsConversationListVisible), nameof(IsMemberSelectVisible), nameof(IsBackVisible), nameof(EmptyNoticeText), nameof(IsEmptyNoticeVisible))]
    public partial bool IsSelectingMembers { get; set; }

    /// <summary>複数の人を選ぶ画面が新しいグループの名前を決める画面か</summary>
    /// <remarks>false のときは開いているグループへ人を追加する画面</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemberSelectTitle), nameof(MemberConfirmText))]
    public partial bool IsNamingGroup { get; set; }

    /// <summary>新しいグループの名前の入力</summary>
    [ObservableProperty]
    public partial string GroupNameInput { get; set; } = "";

    /// <summary>複数の人を選ぶ画面の題名</summary>
    public string MemberSelectTitle => IsNamingGroup ? "新しいグループ" : "人を追加";

    /// <summary>複数の人を選ぶ画面の確定ボタンの文字</summary>
    /// <remarks>新しいグループは会話をまだ作らないので「次へ」</remarks>
    public string MemberConfirmText => IsNamingGroup ? "次へ" : "追加";

    /// <summary>選んでいる人数の文字</summary>
    public string SelectedMembersText => $"参加者 {People.Count(person => person.IsSelected)} 人";

    /// <summary>人数の上限の注意書き</summary>
    public static string MemberLimitText => $"自分を含めて {MessageLimits.GroupMaxMembers} 人までです";

    /// <summary>会話の一覧を出すか</summary>
    /// <remarks>人を選ぶ画面でないとき</remarks>
    public bool IsConversationListVisible => !IsPickingPerson && !IsSelectingMembers;

    /// <summary>人の一覧を出すか</summary>
    /// <remarks>人を選ぶ画面のとき</remarks>
    public bool IsPeopleListVisible => IsPickingPerson;

    /// <summary>複数の人を選ぶ画面を出すか</summary>
    public bool IsMemberSelectVisible => IsSelectingMembers;

    /// <summary>左の上の「戻る」を出すか</summary>
    /// <remarks>人を選ぶ画面か、複数の人を選ぶ画面のとき。</remarks>
    public bool IsBackVisible => IsPickingPerson || IsSelectingMembers;

    /// <summary>開いている会話が作ってあるグループか</summary>
    /// <remarks>会話の上の「参加者」を出すかに使う</remarks>
    [ObservableProperty]
    public partial bool IsGroupOpen { get; set; }

    /// <summary>開いているグループの参加者の名前</summary>
    /// <remarks>自分が先頭</remarks>
    public ObservableCollection<string> Participants { get; } = [];

    /// <summary>参加者の画面の見出し</summary>
    [ObservableProperty]
    public partial string ParticipantsHeaderText { get; set; } = "";

    /// <summary>グループの名前の変更の入力</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameGroupCommand))]
    public partial string RenameInput { get; set; } = "";

    /// <summary>会話が 1 つも無いか</summary>
    /// <remarks>読み込みが済んで、1 件も無いとき。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyNoticeText), nameof(IsEmptyNoticeVisible))]
    public partial bool IsConversationsEmpty { get; set; }

    /// <summary>宛先に選べる人が 1 人もいないか</summary>
    /// <remarks>読み込みが済んで、1 件も無いとき。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyNoticeText), nameof(IsEmptyNoticeVisible))]
    public partial bool IsPeopleEmpty { get; set; }

    /// <summary>一覧が空のときの案内</summary>
    /// <remarks>空でないときは空文字</remarks>
    public string EmptyNoticeText => IsPickingPerson || IsSelectingMembers
        ? (IsPeopleEmpty ? "宛先にできる人がいません。" : "")
        : (IsConversationsEmpty ? "会話はまだありません。「新しい会話」から宛先を選んでください。" : "");

    /// <summary>一覧が空のときの案内を出すか</summary>
    public bool IsEmptyNoticeVisible => EmptyNoticeText.Length > 0;

    /// <summary>会話を開いているか</summary>
    /// <remarks>開いていないときは右に案内を出す</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConversationClosed))]
    public partial bool IsConversationOpen { get; set; }

    /// <summary>会話を開いていないか</summary>
    public bool IsConversationClosed => !IsConversationOpen;

    /// <summary>開いている会話の名前</summary>
    /// <remarks>1 対 1 は相手の表示名</remarks>
    [ObservableProperty]
    public partial string ConversationTitle { get; set; } = "";

    /// <summary>1 対 1 の相手に通知が届くか</summary>
    /// <remarks>届かなくても送れる</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPeerNoNotifyVisible))]
    public partial bool PeerNotifiable { get; set; } = true;

    /// <summary>相手に通知が届かないことの案内を出すか</summary>
    public bool IsPeerNoNotifyVisible => IsConversationOpen && !PeerNotifiable;

    /// <summary>相手に通知が届かないことの案内の文</summary>
    public static string PeerNoNotifyText => MessageDraft.PeerNoNotifyMessage;

    /// <summary>入力した本文</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Body { get; set; } = "";

    /// <summary>返信の元にするメッセージ</summary>
    /// <remarks>返信でなければ null</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReplyTo))]
    [NotifyPropertyChangedFor(nameof(ReplyToText))]
    public partial ChatMessageItem? ReplyTo { get; set; }

    /// <summary>返信の元を出すか</summary>
    public bool HasReplyTo => ReplyTo is not null;

    /// <summary>返信の元の送った人の名前と本文の 1 行目</summary>
    public string ReplyToText => ReplyTo is { } reply ? $"{(reply.IsMine ? "自分" : reply.SenderText)}  {FirstLine(reply.Body)}" : "";

    /// <summary>送信中か</summary>
    /// <remarks>送信中は続けて押せない</remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    /// <summary>メッセージ欄に読み込み中の仮の行を出すか</summary>
    /// <remarks>端末の控えが無い会話を DB から読んでいる間だけ</remarks>
    [ObservableProperty]
    public partial bool IsSkeletonVisible { get; set; }

    /// <summary>最新でない可能性を知らせるか</summary>
    /// <remarks>読み込みに失敗して、端末の控えだけを出しているとき。</remarks>
    [ObservableProperty]
    public partial bool IsStale { get; set; }

    /// <summary>開いている会話の番号</summary>
    /// <remarks>まだ会話が無いときと開いていないときは 0</remarks>
    private int _conversationId;

    /// <summary>空の会話の画面の 1 対 1 の相手の番号</summary>
    /// <remarks>会話があるときは 0</remarks>
    private int _draftPeerUserId;

    /// <summary>空のグループの画面のグループの名前</summary>
    /// <remarks>会話があるときとグループでないときは空文字</remarks>
    private string _draftGroupName = "";

    /// <summary>空のグループの画面の参加者の番号</summary>
    /// <remarks>自分は含めない</remarks>
    private IReadOnlyList<int> _draftGroupMemberIds = [];

    /// <summary>人を追加する画面で、すでに入っている人数</summary>
    /// <remarks>
    /// 自分を含む
    /// 上限を超える追加を押せなくするために使う
    /// </remarks>
    private int _existingMemberCount;

    /// <summary>開いた会話の相手が受け取れるかを DB から読み直したか</summary>
    /// <remarks>
    /// 読み直したあとは控えの要約の値で上書きしない。
    /// 差分の取得は新しいメッセージのあった会話の要約しか返さないので、相手が通知先を変えても、控えの要約は古いままになるため。
    /// </remarks>
    private bool _peerChecked;

    /// <summary>これより古いメッセージがまだあるか</summary>
    private bool _hasMore;

    /// <summary>古いメッセージを読み足している最中か</summary>
    private bool _loadingOlder;

    /// <summary>ウィンドウが前面にありページが見えているか</summary>
    private bool _visible;

    /// <summary>人を選ぶ画面に入るたびに進む番号</summary>
    /// <remarks>古い読み込みの結果を使わないために使う</remarks>
    private int _pickVersion;

    /// <summary>会話を開くたびに進む番号</summary>
    /// <remarks>古い会話の読み込みの結果を使わないために使う</remarks>
    private int _openVersion;

    /// <summary>通知を押して開くとき、位置を合わせるメッセージの番号</summary>
    /// <remarks>無ければ 0</remarks>
    private int _highlightId;

    /// <summary>読み込みの失敗としていま出しているエラーの文</summary>
    /// <remarks>
    /// 読み込みの成功で、これだけを消す。
    /// 出していなければ null。
    /// </remarks>
    private string? _syncErrorText;

    /// <summary>ページを開いたときに呼ぶ</summary>
    /// <returns>始めたことを表すタスク</returns>
    /// <remarks>読み込みの通知を受け始め、会話の一覧を読む。</remarks>
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
        sync.ConversationLeft -= OnConversationLeft;
        sync.ConversationLeft += OnConversationLeft;
        RestoreAsync().Forget();
        RefreshConversations();
        await sync.SyncAsync().ConfigureAwait(true);
    }

    /// <summary>引き継いだ状態から、開いていた会話・人を選ぶ画面・入力中の本文・返信先を元に戻す</summary>
    /// <returns>元に戻す処理の完了を表すタスク</returns>
    /// <remarks>
    /// ページを作り直したあと (外す・戻す・機能をオフにしてオンに戻す)だけ戻す。
    /// すでに開いているページを開き直したときは何もしない。
    /// 会話を開く処理 (<see cref="OpenAsync(int, int, string, bool, string, IReadOnlyList{int})"/>)は本文と返信先を空にして、状態クラスにも書くので、先に引き継いだ値を控える。
    /// 返信先は会話のメッセージを読み込んだあとに、番号で探して戻す。
    /// 見つからなければ、返信はなくなる。
    /// </remarks>
    private async Task RestoreAsync()
    {
        var pickPerson = state.IsPickingPerson && !IsPickingPerson;
        var open = state.IsConversationOpen && !IsConversationOpen;
        var body = state.Body;
        var replyToId = state.ReplyToId;

        var opening = Task.CompletedTask;
        if (open)
        {
            opening = OpenAsync(
                state.ConversationId, state.DraftPeerUserId, state.ConversationTitle, state.PeerNotifiable, state.DraftGroupName, state.DraftGroupMemberIds);
            Body = body;
        }
        if (pickPerson)
        {
            await PickPersonAsync().ConfigureAwait(true);
        }
        await opening.ConfigureAwait(true);

        if (open && replyToId > 0)
        {
            ReplyTo = Messages.FirstOrDefault(message => message.Id == replyToId);
        }
    }

    /// <summary>宛先の人を選ぶ画面にしているかが変わったら状態クラスに覚える</summary>
    /// <param name="value">人を選ぶ画面にしているか</param>
    partial void OnIsPickingPersonChanged(bool value) => state.IsPickingPerson = value;

    /// <summary>会話を開いているかが変わったら状態クラスに覚える</summary>
    /// <param name="value">会話を開いているか</param>
    partial void OnIsConversationOpenChanged(bool value) => state.IsConversationOpen = value;

    /// <summary>開いている会話の名前が変わったら状態クラスに覚える</summary>
    /// <param name="value">会話の名前</param>
    partial void OnConversationTitleChanged(string value)
    {
        state.ConversationTitle = value;
        RenameInput = value;
    }

    /// <summary>新しいグループの名前の入力が変わったら、「次へ」を押せるかを直す</summary>
    /// <param name="value">入力した名前</param>
    partial void OnGroupNameInputChanged(string value) => ConfirmMembersCommand.NotifyCanExecuteChanged();

    /// <summary>相手が通知先を指定しているかが変わったら状態クラスに覚える</summary>
    /// <param name="value">指定しているか</param>
    partial void OnPeerNotifiableChanged(bool value) => state.PeerNotifiable = value;

    /// <summary>入力中の本文が変わったら状態クラスに覚える</summary>
    /// <param name="value">本文</param>
    partial void OnBodyChanged(string value) => state.Body = value;

    /// <summary>返信の元が変わったら状態クラスに覚える</summary>
    /// <param name="value">返信の元のメッセージ (返信でなければ null)</param>
    partial void OnReplyToChanged(ChatMessageItem? value) => state.ReplyToId = value?.Id ?? 0;

    /// <summary>ページが見える・見えないの変化を伝える</summary>
    /// <param name="visible">ウィンドウが前面にあり、ページが見えているか。</param>
    /// <remarks>読む間隔を決めるために使う</remarks>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        sync.SetViewState(IsConversationOpen ? _conversationId : 0, visible);
        if (visible && IsConversationOpen)
        {
            // 相手が通知先を指定したときに、案内を消す
            RefreshPeerNotifiableAsync().Forget();

            // ウィンドウが隠れている間に届いて、画面に出ていたメッセージを、前面に戻ったここで読んだことにする
            ReportRead();
        }
    }

    /// <summary>開いている 1 対 1 の会話の相手に通知が届くかを DB から読み直す</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    /// <remarks>
    /// 読めなかったときは前の値のまま。
    /// 案内が出るかどうかだけで、送れるかは変わらない。
    /// 会話が一覧の要約にまだ無いとき (通知から開いた直後など)は読まない。
    /// 要約が入ったときに <see cref="RefreshConversations"/> がもう一度呼ぶ。
    /// </remarks>
    private async Task RefreshPeerNotifiableAsync()
    {
        var version = _openVersion;
        var conversationId = _conversationId;
        var peerUserId = conversationId > 0
            ? sync.Conversations.FirstOrDefault(conversation => conversation.Id == conversationId)?.PeerUserId ?? 0
            : _draftPeerUserId;
        if (peerUserId <= 0)
        {
            return;
        }

        try
        {
            var receiveState = await repository.GetPeerNotifyStateAsync(conversationId, peerUserId);
            if (receiveState is not null && version == _openVersion)
            {
                _peerChecked = true;
                PeerNotifiable = receiveState.PeerNotifiable;
                if (conversationId > 0)
                {
                    await sync.UpdatePeerNotifiableAsync(conversationId, receiveState.PeerNotifiable);
                }
            }
        }
        catch (DataFileException)
        {
            // 読めなかった。案内の出し入れだけなので、前の値のまま
        }
    }

    /// <summary>通知ダイアログのクリックなどから会話を開く</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <param name="messageId">
    /// 位置を合わせるメッセージの番号
    /// 無ければ 0
    /// </param>
    /// <returns>開く処理の完了を表すタスク</returns>
    public async Task OpenConversationAsync(int conversationId, int messageId = 0)
    {
        _highlightId = messageId;
        var summary = sync.Conversations.FirstOrDefault(conversation => conversation.Id == conversationId);
        await OpenAsync(conversationId, 0, summary?.Name ?? "メッセージ", summary?.PeerNotifiable ?? true).ConfigureAwait(true);
    }

    /// <summary>人の一覧から会話を開く</summary>
    /// <param name="item">選んだ行</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>
    /// すでに開いている会話の行を押したときは何もしない。
    /// メッセージの表示と、入力中の本文・返信先を、そのまま残すため。
    /// </remarks>
    public Task OpenAsync(ConversationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IsConversationOpen && _conversationId == item.Id)
        {
            return Task.CompletedTask;
        }

        _highlightId = 0;
        return OpenAsync(item.Id, 0, item.Name, item.Source.PeerNotifiable);
    }

    /// <summary>選んだ人との会話を開く</summary>
    /// <param name="person">選んだ人</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>
    /// まだ会話が無いときは空の会話の画面を出す。
    /// 開けたら、会話の一覧へ戻る。
    /// </remarks>
    [RelayCommand]
    private async Task OpenPersonAsync(PersonItem person)
    {
        try
        {
            var conversationId = await repository.FindDirectConversationAsync(person.Source.UserId);
            _highlightId = 0;
            await OpenAsync(conversationId, person.Source.UserId, person.DisplayName, peerNotifiable: true);
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

    /// <summary>本文のリンクを既定のブラウザで開く</summary>
    /// <param name="href">
    /// 開く URL
    /// 本文から見つけた <c>http</c> か <c>https</c>
    /// </param>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>開けなかったときは失敗を表示する</remarks>
    [RelayCommand]
    private async Task OpenLinkAsync(string href)
    {
        try
        {
            await opener.OpenAsync(href);
        }
        catch (PathOpenException ex)
        {
            Error.Show($"リンクを開けませんでした: {ex.Message}");
        }
    }

    /// <summary>古いメッセージを 50 件読み足す</summary>
    /// <returns>読み足しの完了を表すタスク</returns>
    /// <remarks>画面が先頭に近づいたときに呼ぶ</remarks>
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

    /// <summary>読み込みの通知を受けるのをやめる</summary>
    /// <remarks>
    /// ページが外れたときに呼ぶ
    /// 次に開くとまた受け始める
    /// </remarks>
    public void Detach()
    {
        sync.Synced -= OnSynced;
        sync.MessagesArrived -= OnMessagesArrived;
        sync.ConversationLeft -= OnConversationLeft;
    }

    /// <summary>別の端末でグループを抜けたことが届いたとき、開いていた会話を閉じる</summary>
    /// <param name="conversationId">抜けた会話の番号</param>
    private void OnConversationLeft(int conversationId) => Post(() =>
    {
        if (IsConversationOpen && _conversationId == conversationId)
        {
            CloseConversation();
        }
    });

    /// <summary>開いている会話を閉じて右の案内に戻す</summary>
    private void CloseConversation()
    {
        _openVersion++;
        _conversationId = 0;
        _draftPeerUserId = 0;
        _draftGroupName = "";
        _draftGroupMemberIds = [];
        state.ConversationId = 0;
        state.DraftPeerUserId = 0;
        state.DraftGroupName = "";
        state.DraftGroupMemberIds = [];
        UpdateGroupFlags();
        IsConversationOpen = false;
        IsSkeletonVisible = false;
        Body = "";
        ReplyTo = null;
        ReplaceMessages([]);
        sync.SetViewState(0, _visible);
        OnPropertyChanged(nameof(SelectedConversation));
    }

    /// <inheritdoc />
    public void Dispose() => Detach();

    /// <summary>人を選ぶ画面に入る</summary>
    /// <returns>読む処理の完了を表すタスク</returns>
    /// <remarks>宛先に選べる人を開くたびに読み直す</remarks>
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

    /// <summary>人を選ぶ画面と複数の人を選ぶ画面から会話の一覧へ戻る</summary>
    [RelayCommand]
    private void CancelPickPerson()
    {
        _pickVersion++;
        IsPickingPerson = false;
        IsSelectingMembers = false;
        GroupNameInput = "";
        People.Clear();
        OnPropertyChanged(nameof(SelectedMembersText));
    }

    /// <summary>新しいグループを作る画面に入る</summary>
    /// <returns>人の一覧を読む処理の完了を表すタスク</returns>
    /// <remarks>名前と参加者を複数選ぶ</remarks>
    [RelayCommand]
    private Task StartGroupAsync()
    {
        IsNamingGroup = true;
        GroupNameInput = "";
        return LoadMemberCandidatesAsync([]);
    }

    /// <summary>開いているグループへ人を追加する画面に入る</summary>
    /// <returns>人の一覧を読む処理の完了を表すタスク</returns>
    /// <remarks>すでに入っている人は除く</remarks>
    [RelayCommand]
    private Task StartAddMembersAsync()
    {
        if (sync.Conversations.FirstOrDefault(conversation => conversation.Id == _conversationId) is not { Kind: ConversationKind.Group } summary)
        {
            return Task.CompletedTask;
        }

        IsNamingGroup = false;
        _existingMemberCount = summary.Readers.Count + 1;
        return LoadMemberCandidatesAsync([.. summary.Readers.Select(reader => reader.UserId)]);
    }

    /// <summary>複数の人を選ぶ画面に入り、選べる人を読む</summary>
    /// <param name="excludeIds">
    /// 一覧に出さない人の番号
    /// 自分はいつも出さない
    /// </param>
    /// <returns>読む処理の完了を表すタスク</returns>
    /// <remarks>開くたびに読み直す</remarks>
    private async Task LoadMemberCandidatesAsync(IReadOnlyCollection<int> excludeIds)
    {
        var version = ++_pickVersion;
        People.Clear();
        IsPeopleEmpty = false;
        IsPickingPerson = false;
        IsSelectingMembers = true;
        OnPropertyChanged(nameof(SelectedMembersText));
        ConfirmMembersCommand.NotifyCanExecuteChanged();

        try
        {
            var found = await repository.SearchPeopleAsync("", SearchCount);
            if (version != _pickVersion)
            {
                return;
            }

            foreach (var person in found.Where(person => person.UserId != sync.UserId && !excludeIds.Contains(person.UserId)))
            {
                var item = new PersonItem(person);
                item.PropertyChanged += OnCandidatePropertyChanged;
                People.Add(item);
            }
            IsPeopleEmpty = People.Count == 0;
        }
        catch (DataFileException ex)
        {
            Error.Show($"宛先の人を読み込めませんでした: {ex.Message}");
        }
    }

    /// <summary>選べる人のチェックが変わったら人数と確定ボタンを直す</summary>
    /// <param name="sender">チェックが変わった人</param>
    /// <param name="e">変わったプロパティの情報</param>
    private void OnCandidatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PersonItem.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedMembersText));
            ConfirmMembersCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>複数の人を選ぶ画面の確定ボタンを押せるか</summary>
    /// <returns>
    /// 新しいグループは名前が使え、参加者が 1 人以上で自分を含めて上限以内のとき true。
    /// 人の追加は 1 人以上で、すでに入っている人と合わせて上限以内のとき true。
    /// </returns>
    private bool CanConfirmMembers()
    {
        var count = People.Count(person => person.IsSelected);
        return IsNamingGroup
            ? MessageDraft.IsValidGroupName(GroupNameInput) && MessageDraft.IsValidGroupMemberCount(count)
            : count >= 1 && _existingMemberCount + count <= MessageLimits.GroupMaxMembers;
    }

    /// <summary>複数の人を選ぶ画面を確定する</summary>
    /// <returns>確定の処理の完了を表すタスク</returns>
    /// <remarks>
    /// 新しいグループは空のグループの画面を開き、人の追加はグループへ追加する。
    /// 新しいグループは会話をまだ作らない。
    /// 最初の送信で作る。
    /// メッセージの無い会話を DB に残さないため。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanConfirmMembers))]
    private async Task ConfirmMembersAsync()
    {
        var ids = People.Where(person => person.IsSelected).Select(person => person.Source.UserId).ToList();
        if (IsNamingGroup)
        {
            var name = GroupNameInput.Trim();
            _highlightId = 0;
            CancelPickPerson();
            await OpenAsync(0, 0, name, peerNotifiable: true, name, ids);
            return;
        }

        try
        {
            var outcome = await repository.AddMembersAsync(_conversationId, ids);
            if (outcome == GroupChangeOutcome.Done)
            {
                CancelPickPerson();
                await sync.SyncAsync();
                return;
            }
            Error.Show($"追加できませんでした: {GroupChangeText(outcome)}");
        }
        catch (DataFileException ex)
        {
            Error.Show($"追加できませんでした: {ex.Message}");
        }
    }

    /// <summary>グループの名前を変えられるか</summary>
    /// <returns>作ってあるグループを開いていて、新しい名前が使え、今の名前と違うとき true。</returns>
    private bool CanRenameGroup() => IsGroupOpen && MessageDraft.IsValidGroupName(RenameInput) && RenameInput.Trim() != ConversationTitle;

    /// <summary>グループの名前を変える</summary>
    /// <returns>変える処理の完了を表すタスク</returns>
    /// <remarks>ほかの参加者にはシステムメッセージで届く</remarks>
    [RelayCommand(CanExecute = nameof(CanRenameGroup))]
    private async Task RenameGroupAsync()
    {
        try
        {
            var outcome = await repository.RenameGroupAsync(_conversationId, RenameInput);
            if (outcome == GroupChangeOutcome.Done)
            {
                await sync.SyncAsync();
                return;
            }
            Error.Show($"名前を変えられませんでした: {GroupChangeText(outcome)}");
        }
        catch (DataFileException ex)
        {
            Error.Show($"名前を変えられませんでした: {ex.Message}");
        }
    }

    /// <summary>グループを抜ける</summary>
    /// <returns>抜ける処理の完了を表すタスク</returns>
    /// <remarks>
    /// 確認のあと自分の参加を外す。
    /// 他の人を外す操作は無い。
    /// 抜けたあとは会話を読めず、もう一度入るには参加者に追加してもらう。
    /// </remarks>
    [RelayCommand]
    private async Task LeaveGroupAsync()
    {
        if (!IsGroupOpen
            || !await dialogs.ConfirmAsync(
                "グループを抜けますか？",
                "抜けると、このグループのメッセージを読めなくなります。もう一度入るには、参加者に追加してもらう必要があります。",
                "抜ける",
                "キャンセル"))
        {
            return;
        }

        try
        {
            var outcome = await repository.LeaveGroupAsync(_conversationId);
            if (outcome == GroupChangeOutcome.Done)
            {
                CloseConversation();
                await sync.SyncAsync();
                return;
            }
            Error.Show($"グループを抜けられませんでした: {GroupChangeText(outcome)}");
        }
        catch (DataFileException ex)
        {
            Error.Show($"グループを抜けられませんでした: {ex.Message}");
        }
    }

    /// <summary>グループの変更が失敗した理由を画面に出す文にする</summary>
    /// <param name="outcome">結果の区分</param>
    /// <returns>理由の文</returns>
    private static string GroupChangeText(GroupChangeOutcome outcome) => outcome switch
    {
        GroupChangeOutcome.Invalid => "名前か人数が正しくありません。",
        GroupChangeOutcome.NotGroup => "グループではない会話です。",
        _ => "会話が見つかりません。",
    };

    /// <summary>開いている会話が作ってあるグループかを調べ、参加者の一覧と名前の変更の入力を合わせる</summary>
    private void UpdateGroupFlags()
    {
        var summary = _conversationId > 0 ? sync.Conversations.FirstOrDefault(conversation => conversation.Id == _conversationId) : null;
        IsGroupOpen = summary is { Kind: ConversationKind.Group };

        List<string> names = summary is { Kind: ConversationKind.Group } group ? ["自分", .. group.Readers.Select(reader => reader.DisplayName)] : [];
        if (!Participants.SequenceEqual(names))
        {
            Participants.Clear();
            foreach (var name in names)
            {
                Participants.Add(name);
            }
        }
        ParticipantsHeaderText = names.Count == 0 ? "" : $"参加者 {names.Count} 人";
        RenameGroupCommand.NotifyCanExecuteChanged();
    }

    /// <summary>会話を開く</summary>
    /// <param name="conversationId">
    /// 会話の番号
    /// まだ会話が無いときは 0
    /// </param>
    /// <param name="peerUserId">会話の番号が 0 のとき、1 対 1 の相手の番号。</param>
    /// <param name="title">会話の名前</param>
    /// <param name="peerNotifiable">相手が通知先を指定しているか</param>
    /// <param name="groupName">
    /// 会話の番号が 0 のとき、新しいグループの名前。
    /// グループでなければ空文字。
    /// </param>
    /// <param name="groupMemberIds">
    /// 会話の番号が 0 のとき、新しいグループの参加者の番号。
    /// 自分は含めない。
    /// </param>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>控えから出して、続けて DB から最新を読む。</remarks>
    private async Task OpenAsync(
        int conversationId, int peerUserId, string title, bool peerNotifiable, string groupName = "", IReadOnlyList<int>? groupMemberIds = null)
    {
        var version = ++_openVersion;
        _conversationId = conversationId;
        _draftPeerUserId = conversationId == 0 ? peerUserId : 0;
        _draftGroupName = conversationId == 0 ? groupName : "";
        _draftGroupMemberIds = conversationId == 0 ? groupMemberIds ?? [] : [];
        state.ConversationId = _conversationId;
        state.DraftPeerUserId = _draftPeerUserId;
        state.DraftGroupName = _draftGroupName;
        state.DraftGroupMemberIds = [.. _draftGroupMemberIds];
        UpdateGroupFlags();
        ConversationTitle = title;
        PeerNotifiable = peerNotifiable;
        IsConversationOpen = true;
        OnPropertyChanged(nameof(SelectedConversation));
        _peerChecked = false;
        RefreshPeerNotifiableAsync().Forget();
        IsStale = false;
        Body = "";
        ReplyTo = null;
        _hasMore = false;
        Error.Clear();
        sync.SetViewState(conversationId, _visible);
        IsSkeletonVisible = false;
        var preparing = PrepareSwitchAsync();
        if (conversationId == 0)
        {
            await preparing;
            if (version == _openVersion)
            {
                ReplaceMessages([]);
            }
            return;
        }

        // まず控えから出す。前の会話の見た目を撮り終え、控えも読み終えるまで待ち、1 度に差し替える。空の画面を挟まない
        var cached = await sync.ReadCachedMessagesAsync(conversationId);
        await preparing;
        if (version != _openVersion)
        {
            return;
        }
        ReplaceMessages(cached);

        // 控えが無いときだけ、読み終えるまで仮の行を出す
        IsSkeletonVisible = cached.Count == 0;

        try
        {
            var newest = await sync.LoadMessagesAsync(conversationId, 0, MessageSync.PageSize + 1);
            if (version != _openVersion)
            {
                return;
            }

            IsSkeletonVisible = false;
            MergeNewest(newest.Take(MessageSync.PageSize).Reverse().ToList(), newest.Count > MessageSync.PageSize);
            await sync.WriteCachedMessagesAsync(conversationId, [.. Messages.Select(item => item.Source)]);
            ReportRead();
        }
        catch (DataFileException ex)
        {
            // 読めないときは、控えを、読むだけで出す
            IsSkeletonVisible = false;
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

    /// <summary>DB から読んだ最新の 1 ページを控えと合わせる</summary>
    /// <param name="fetched">
    /// 読んだメッセージ
    /// 古いものが先
    /// </param>
    /// <param name="pageHasMore">読んだページより古いメッセージがまだあるか</param>
    /// <remarks>控えは新しいほうから切れ目なく持つ</remarks>
    private void MergeNewest(IReadOnlyList<ChatMessage> fetched, bool pageHasMore)
    {
        if (Messages.Count > 0 && fetched.Count > 0 && fetched[0].Id <= Messages[^1].Id)
        {
            // 控えと重なる。番号で突き合わせて、変わった行だけ差し替え、無い行だけ番号の順に挿す (全部を消して並べ直さない)
            foreach (var message in fetched)
            {
                UpsertMessage(message);
            }
            _hasMore = pageHasMore || Messages.Count > fetched.Count;
            return;
        }

        // 控えと重ならない。間が抜けるので、控えを捨てて、読んだページだけにする
        ReplaceMessages(fetched);
        _hasMore = pageHasMore;
    }

    /// <summary>開いている会話のメッセージを丸ごと差し替える</summary>
    /// <param name="items">
    /// 差し替え後のメッセージ
    /// 古いものが先
    /// </param>
    /// <remarks>差し替えたあと画面に知らせる</remarks>
    private void ReplaceMessages(IEnumerable<ChatMessage> items)
    {
        Messages.Clear();
        foreach (var message in items)
        {
            Messages.Add(ToItem(message));
        }
        MessagesReplaced?.Invoke();
    }

    /// <summary>メッセージを 1 件番号の順を保って取り込む</summary>
    /// <param name="message">取り込むメッセージ</param>
    /// <returns>
    /// 新しい行を挿したとき true。
    /// 既にある行の更新や、古くて捨てたときは false。
    /// </returns>
    /// <remarks>あれば、新しいときだけ差し替え、無ければ挿す。</remarks>
    private bool UpsertMessage(ChatMessage message)
    {
        var index = 0;
        while (index < Messages.Count && Messages[index].Id < message.Id)
        {
            index++;
        }

        if (index < Messages.Count && Messages[index].Id == message.Id)
        {
            var current = Messages[index].Source;
            if (message.UpdatedAt >= current.UpdatedAt && !message.Equals(current))
            {
                Messages[index] = ToItem(message);
            }
            return false;
        }

        Messages.Insert(index, ToItem(message));
        return true;
    }

    /// <summary>読み込みを 1 回終えたとき、人の一覧とエラーを画面に合わせる</summary>
    private void OnSynced() => Post(() =>
    {
        RefreshConversations();
        RefreshReadMarks();

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
    /// <param name="arrived">届いた、または更新されたメッセージ。</param>
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
            added |= UpsertMessage(message);
        }

        if (added)
        {
            ScrollToEndRequested?.Invoke(true);
        }
        sync.WriteCachedMessagesAsync(_conversationId, [.. Messages.Select(item => item.Source)]).Forget();
        ReportRead();
    });

    /// <summary>人の一覧を読み続ける部品が持つ要約に合わせる</summary>
    /// <remarks>変わった行だけ差し替える</remarks>
    private void RefreshConversations()
    {
        var summaries = sync.Conversations;
        for (var i = 0; i < summaries.Count; i++)
        {
            if (i >= Conversations.Count)
            {
                Conversations.Add(new ConversationItem(summaries[i], sync.UserId));
            }
            else if (!Conversations[i].Source.Equals(summaries[i]))
            {
                Conversations[i] = new ConversationItem(summaries[i], sync.UserId);
            }
        }
        while (Conversations.Count > summaries.Count)
        {
            Conversations.RemoveAt(Conversations.Count - 1);
        }

        IsConversationsEmpty = Conversations.Count == 0;
        if (!_peerChecked && IsConversationOpen && _conversationId > 0 && summaries.FirstOrDefault(conversation => conversation.Id == _conversationId) is { } open)
        {
            // 要約の値は古いことがある。今の値を DB から読み直す
            PeerNotifiable = open.PeerNotifiable;
            RefreshPeerNotifiableAsync().Forget();
        }

        if (IsConversationOpen && _conversationId > 0 && summaries.FirstOrDefault(conversation => conversation.Id == _conversationId) is { } current
            && current.Name != ConversationTitle)
        {
            // 別の参加者が、グループの名前を変えた
            ConversationTitle = current.Name;
        }
        UpdateGroupFlags();

        // 行を差し替えたり、新しい会話の行が増えたりしたので、選びの印を付け直す
        OnPropertyChanged(nameof(SelectedConversation));
    }

    /// <summary>送れる状態か</summary>
    /// <returns>送れるとき true</returns>
    /// <remarks>会話を開いていて、送っている最中でなく、本文が空白だけでない。</remarks>
    private bool CanSend() => IsConversationOpen && !IsSending && MessageDraft.CanSend(Body);

    /// <summary>メッセージを送る</summary>
    /// <returns>送る処理の完了を表すタスク</returns>
    /// <remarks>
    /// メッセージと通知の状態の行を DB に書く。
    /// 続けられる失敗なので、失敗は画面に出す。
    /// 送れたら、本文と返信を空に戻す。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        IsSending = true;
        try
        {
            var target = _conversationId > 0 ? MessageTarget.Existing(_conversationId)
                : _draftGroupName.Length > 0 ? MessageTarget.NewGroup(_draftGroupName, _draftGroupMemberIds)
                : MessageTarget.Direct(_draftPeerUserId);
            var result = await repository.SendAsync(target, Body, ReplyTo?.Id ?? 0);
            if (result.Outcome != SendMessageOutcome.Sent)
            {
                Error.Show($"送れませんでした: {OutcomeText(result.Outcome)}");
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
                _draftGroupName = "";
                _draftGroupMemberIds = [];
                state.ConversationId = _conversationId;
                state.DraftPeerUserId = 0;
                state.DraftGroupName = "";
                state.DraftGroupMemberIds = [];
                OnPropertyChanged(nameof(SelectedConversation));
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

    /// <summary>送れなかった理由を画面に出す文にする</summary>
    /// <param name="outcome">結果の区分</param>
    /// <returns>理由の文</returns>
    private static string OutcomeText(SendMessageOutcome outcome) => outcome switch
    {
        SendMessageOutcome.ReplyInvalid => "返信元のメッセージが見つかりません。",
        SendMessageOutcome.GroupInvalid => "グループの名前か参加者が正しくありません。",
        _ => "会話が見つかりません。",
    };

    /// <summary>メッセージを画面の行にする</summary>
    /// <param name="message">メッセージ</param>
    /// <returns>
    /// 画面の行
    /// 自分が送ったメッセージかは読み込んだ人の番号で決める
    /// </returns>
    private ChatMessageItem ToItem(ChatMessage message)
    {
        var (readText, readerNames) = ReadInfoOf(message);
        return new ChatMessageItem(message, message.SenderUserId == sync.UserId, OpenLinkCommand, readText, ShowSenderOf(message), readerNames);
    }

    /// <summary>送った人の名前を吹き出しの上に出すか</summary>
    /// <param name="message">メッセージ</param>
    /// <returns>
    /// グループで、他の人が送ったメッセージのとき true。
    /// 会話の要約がまだ無いときは false。
    /// </returns>
    private bool ShowSenderOf(ChatMessage message)
        => !message.IsSystem && message.SenderUserId != sync.UserId
        && sync.Conversations.FirstOrDefault(conversation => conversation.Id == message.ConversationId) is { Kind: ConversationKind.Group };

    /// <summary>自分が送ったメッセージに付ける、相手が読んだことを表す文字と読んだ人の名前を作る</summary>
    /// <param name="message">メッセージ</param>
    /// <returns>
    /// 文字は 1 対 1 は「既読」、グループは「既読 N」。
    /// N は読んだ人数で、送った人を含めない。
    /// 名前はグループで読んだ人がいるときだけ、1 行に 1 人。
    /// 読まれていない・自分が送ったのでない・システムメッセージ・会話の要約がまだ無いときはどちらも空文字。
    /// </returns>
    /// <remarks>読んだとは参加者の読んだ位置が、メッセージの番号以上のこと。</remarks>
    private (string Text, string Names) ReadInfoOf(ChatMessage message)
    {
        if (message.SenderUserId != sync.UserId || message.IsSystem
            || sync.Conversations.FirstOrDefault(conversation => conversation.Id == message.ConversationId) is not { } summary)
        {
            return ("", "");
        }

        var readers = summary.Readers.Where(reader => reader.UserId != message.SenderUserId && reader.LastReadMessageId >= message.Id).ToList();
        if (readers.Count == 0)
        {
            return ("", "");
        }

        return summary.Kind == ConversationKind.Direct
            ? ("既読", "")
            : ($"既読 {readers.Count}", string.Join('\n', readers.Select(reader => reader.DisplayName)));
    }

    /// <summary>開いている会話のメッセージの「既読」と送った人の名前を読み続ける部品が持つ要約に合わせる</summary>
    /// <remarks>変わった行だけ差し替える</remarks>
    private void RefreshReadMarks()
    {
        for (var i = 0; i < Messages.Count; i++)
        {
            var (readText, readerNames) = ReadInfoOf(Messages[i].Source);
            if (Messages[i].ReadText != readText || Messages[i].ReaderNames != readerNames || Messages[i].HasSender != ShowSenderOf(Messages[i].Source))
            {
                Messages[i] = ToItem(Messages[i].Source);
            }
        }
    }

    /// <summary>開いている会話の一番新しいメッセージまで読んだことを DB に書く</summary>
    /// <remarks>
    /// 会話を開いていて、ウィンドウが前面に見えているときだけ書く。
    /// 裏に回っている間や、通知ダイアログに出しただけでは読んだことにしない。
    /// 書けなかったときは次の機会にもう一度書く。
    /// </remarks>
    private void ReportRead()
    {
        if (!_visible || !IsConversationOpen || _conversationId == 0 || Messages.Count == 0)
        {
            return;
        }

        sync.MarkReadAsync(_conversationId, Messages[^1].Id).Forget();
    }

    /// <summary>本文の 1 行目を取る</summary>
    /// <param name="text">本文</param>
    /// <returns>最初の改行より前の文字</returns>
    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? text : text[..end];
    }
}
