namespace MmmToolWeb.Messages.Client;

/// <summary>
/// 会話の中の 1 通。
/// </summary>
/// <param name="Id">メッセージの番号。</param>
/// <param name="ConversationId">会話の番号。</param>
/// <param name="SenderUserId">送った人の番号。0 はシステムメッセージ。</param>
/// <param name="SenderName">送った人の表示名。</param>
/// <param name="IsMine">自分が送ったメッセージか。サーバーが、自分の番号と比べて決める。</param>
/// <param name="ReplyToMessageId">返信元の番号。返信でなければ 0。</param>
/// <param name="ReplyToSenderName">返信元を送った人の表示名。返信でない、または読めないときは null。</param>
/// <param name="ReplyToText">返信元の本文の 1 行目。返信でない、または読めないときは null。</param>
/// <param name="Body">本文。</param>
/// <param name="CreatedAt">送った日時。並びの決めと、差分の取り合わせには使わず、時刻の表示の元にする。</param>
/// <param name="TimeText">送った日時の表示の文字。サーバーの時計で作る。</param>
/// <param name="UpdatedAt">最後に更新した日時。同じメッセージが何度届いても、結果が変わらないように、突き合わせに使う。</param>
/// <param name="Links">本文の中の、リンクにする URL。サーバーが見つける。端末の控えに残っている古いメッセージは null で、本文をそのまま出す。</param>
public sealed record MessageDto(
    int Id, int ConversationId, int SenderUserId, string SenderName, bool IsMine, int ReplyToMessageId, string? ReplyToSenderName, string? ReplyToText,
    string Body, DateTimeOffset CreatedAt, string TimeText, DateTimeOffset UpdatedAt, IReadOnlyList<MessageLinkDto>? Links = null)
{
    /// <summary>システムメッセージ (人の追加・抜けた・名前の変更)か。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSystem => SenderUserId == 0;
}

/// <summary>
/// 本文の中の、リンクにする URL 1 つ。
/// </summary>
/// <param name="Start">本文の中での始まりの位置。</param>
/// <param name="Length">画面に出す文字の長さ。</param>
/// <param name="Href">開く URL。<c>http</c> か <c>https</c> の絶対 URL。</param>
public sealed record MessageLinkDto(int Start, int Length, string Href);

/// <summary>
/// 人の一覧に出す、会話 1 つの要約。
/// </summary>
/// <param name="Id">会話の番号。</param>
/// <param name="IsDirect">1 対 1 か。</param>
/// <param name="Name">画面に出す名前。1 対 1 は相手の表示名、グループは会話の名前。</param>
/// <param name="PeerUserId">1 対 1 の相手の番号。自分との会話は自分の番号。グループは 0。</param>
/// <param name="PeerCanReceive">1 対 1 の相手が、受け取り先を指定しているか。false のとき、入力欄は押せない。</param>
/// <param name="LastMessageId">最後のメッセージの番号。会話の並びの基準。</param>
/// <param name="LastText">最後のメッセージの本文の 1 行目。</param>
/// <param name="TimeText">最後のメッセージの日時の表示の文字。</param>
/// <param name="UnreadCount">自分がまだ読んでいないメッセージの数。システムメッセージは数えない。会話の行の青い点の元。0 なら読んでいないものは無い。</param>
/// <param name="Readers">自分以外の参加者の、名前と読んだ位置。送った人の画面に「既読」を出す元で、グループの参加者の一覧にも使う。</param>
/// <param name="LastSenderUserId">最後のメッセージを送った人の番号。0 はシステムメッセージ。</param>
/// <param name="LastSenderName">最後のメッセージを送った人の表示名。システムメッセージと、分からないときは空文字。</param>
public sealed record ConversationDto(
    int Id, bool IsDirect, string Name, int PeerUserId, bool PeerCanReceive, int LastMessageId, string LastText, string TimeText,
    int UnreadCount = 0, IReadOnlyList<ReadPositionDto>? Readers = null, int LastSenderUserId = 0, string LastSenderName = "");

/// <summary>
/// 読んでいないメッセージの、全部の会話の合計。
/// </summary>
/// <param name="Count">読んでいないメッセージの数。</param>
public sealed record UnreadCountDto(int Count);

/// <summary>
/// 会話の参加者 1 人の、名前と読んだ位置。
/// </summary>
/// <param name="UserId">参加者の番号。</param>
/// <param name="LastReadMessageId">その人が読んだ最後のメッセージの番号。0 は何も読んでいない。</param>
/// <param name="DisplayName">参加者の表示名。</param>
public sealed record ReadPositionDto(int UserId, int LastReadMessageId, string DisplayName = "");

/// <summary>
/// 読んだ位置を書く要求。
/// </summary>
/// <param name="MessageId">読んだ最後のメッセージの番号。</param>
public sealed record MessageReadDto(int MessageId);

/// <summary>
/// 会話とメッセージの読み込みの結果。最初の読み込みと、差分の取得で共通。
/// </summary>
/// <param name="UserId">自分の番号。端末の控えを、人ごとに分けるために使う。</param>
/// <param name="NextBase">次の差分の取得に送る基準の日時。サーバーが返した値を、そのまま送り返す。</param>
/// <param name="Conversations">変更のあった会話の要約。最初の読み込みでは、自分の会話すべて。</param>
/// <param name="Messages">変更のあったメッセージ。最初の読み込みでは空。</param>
/// <param name="LeftConversationIds">自分が抜けた会話の番号。別の端末で抜けたグループを、この端末の控えと一覧から消す。</param>
public sealed record MessageSyncDto(
    int UserId, DateTimeOffset NextBase, IReadOnlyList<ConversationDto> Conversations, IReadOnlyList<MessageDto> Messages, IReadOnlyList<int> LeftConversationIds);

/// <summary>
/// 会話のメッセージの 1 ページ。
/// </summary>
/// <param name="Messages">メッセージ。古いものが先で、新しいものが後。</param>
/// <param name="HasMore">これより古いメッセージが、まだあるか。</param>
public sealed record MessagePageDto(IReadOnlyList<MessageDto> Messages, bool HasMore);

/// <summary>
/// 送信の要求。
/// </summary>
/// <param name="ConversationId">会話の番号。まだ会話が無いときは 0。</param>
/// <param name="PeerUserId">会話の番号が 0 のとき、1 対 1 の相手の番号。</param>
/// <param name="Body">本文。</param>
/// <param name="ReplyToMessageId">返信元の番号。返信でなければ 0。</param>
/// <param name="GroupName">新しいグループの名前。新しいグループでなければ空文字。</param>
/// <param name="GroupMemberIds">新しいグループの参加者の番号。自分は含めない。新しいグループでなければ空。</param>
/// <remarks>送り先は、会話の番号・相手の番号・新しいグループ (名前と参加者)のどれか 1 つ。</remarks>
public sealed record MessageSendDto(
    int ConversationId, int PeerUserId, string Body, int ReplyToMessageId, string GroupName = "", IReadOnlyList<int>? GroupMemberIds = null);

/// <summary>
/// グループに人を追加する要求。
/// </summary>
/// <param name="UserIds">追加する人の番号。</param>
public sealed record GroupMembersAddDto(IReadOnlyList<int> UserIds);

/// <summary>
/// グループを抜ける要求。中身は無いが、状態を変える操作は JSON の本文を要る形にするため、空の本文を送る。
/// </summary>
public sealed record GroupLeaveDto;

/// <summary>
/// グループの名前を変える要求。
/// </summary>
/// <param name="Name">新しい名前。</param>
public sealed record GroupRenameDto(string Name);

/// <summary>
/// 送れたときの結果。
/// </summary>
/// <param name="ConversationId">書いた会話の番号。</param>
/// <param name="MessageId">書いたメッセージの番号。</param>
public sealed record MessageSentDto(int ConversationId, int MessageId);

/// <summary>
/// 宛先に探した人 1 人。
/// </summary>
/// <param name="UserId">人の番号。</param>
/// <param name="DisplayName">表示名。</param>
public sealed record PersonDto(int UserId, string DisplayName);

/// <summary>
/// 作る途中のグループ。名前と参加者を決めたあと、最初の送信までの間、画面が持つ。API には送らない。
/// </summary>
/// <param name="Name">グループの名前。</param>
/// <param name="MemberIds">参加者の番号。自分は含めない。</param>
public sealed record PendingGroup(string Name, IReadOnlyList<int> MemberIds);

/// <summary>
/// 複数の人を選ぶシートで、確定したときの内容。画面の中だけで使い、API には送らない。
/// </summary>
/// <param name="Name">新しいグループの名前。人を追加するときは空文字。</param>
/// <param name="UserIds">選んだ人の番号。</param>
public sealed record MemberPick(string Name, IReadOnlyList<int> UserIds);

/// <summary>
/// 相手との 1 対 1 の会話の番号。
/// </summary>
/// <param name="ConversationId">会話の番号。まだ無ければ 0。</param>
public sealed record DirectDto(int ConversationId);

/// <summary>
/// 入力欄を出すのに要る情報。
/// </summary>
/// <param name="BodyMaxLength">本文の最大の長さ。</param>
/// <param name="SelfCanReceive">自分が、受け取り先を指定しているか。false のとき、入力欄は押せない。</param>
/// <param name="PeerCanReceive">相手の番号を渡したとき、その相手が、受け取り先を指定しているか。番号を渡さないときは true。false のとき、入力欄は押せない。</param>
/// <param name="GroupNameMaxLength">グループの名前の最大の長さ。</param>
/// <param name="GroupMaxMembers">グループの参加者の最大の人数。自分を含む。</param>
public sealed record MessageFormDto(int BodyMaxLength, bool SelfCanReceive, bool PeerCanReceive, int GroupNameMaxLength, int GroupMaxMembers);

