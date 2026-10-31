namespace MmmTool.Messages.Core;

/// <summary>
/// 人の一覧に出す、会話 1 つの要約。
/// </summary>
/// <param name="Id">会話の番号</param>
/// <param name="Kind">会話の種類</param>
/// <param name="Name">画面に出す名前。1 対 1 は相手の表示名、グループは会話の名前</param>
/// <param name="PeerUserId">1 対 1 の相手の番号。自分との会話は自分の番号。グループは 0</param>
/// <param name="PeerCanReceive">1 対 1 の相手が、受け取り先を指定しているか。false のとき、入力欄は押せない。グループは true</param>
/// <param name="LastMessageId">最後のメッセージの番号。会話の並びの基準になる</param>
/// <param name="LastText">最後のメッセージの本文</param>
/// <param name="LastAt">最後のメッセージを送った日時</param>
/// <param name="LastSenderUserId">最後のメッセージを送った人の番号。0 はシステムメッセージ。グループの一覧で「送った人の名前: 本文」を出す元</param>
/// <param name="LastSenderName">最後のメッセージを送った人の表示名。システムメッセージと、分からないときは空文字</param>
/// <param name="UnreadCount">自分がまだ読んでいないメッセージの数。自分の読んだ位置より新しい、他の人が送ったメッセージ。自分との会話は、自分が送ったものも数える。システムメッセージは数えない。会話の行の青い点と、ホーム画面の数字の元</param>
/// <param name="Readers">自分以外の参加者の、名前と読んだ位置。送った人の画面に「既読」を出す元で、グループの参加者の一覧にも使う。自分との会話は空</param>
/// <remarks>最後のメッセージは、会話の表に持たず、メッセージの表から、会話ごとに最新の 1 通を読んで求める。</remarks>
public sealed record ConversationSummary(
    int Id, ConversationKind Kind, string Name, int PeerUserId, bool PeerCanReceive, int LastMessageId, string LastText, DateTimeOffset LastAt,
    int LastSenderUserId, string LastSenderName, int UnreadCount, IReadOnlyList<ReadPosition> Readers)
{
    /// <summary>同じ内容か。読んだ位置の一覧は、並びと中身で比べる</summary>
    /// <param name="other">比べる要約</param>
    /// <returns>すべての値が同じとき true</returns>
    /// <remarks>
    /// 差分の取得と端末の控えの読み込みのたびに、一覧は別の入れ物になる。入れ物で比べると、同じ内容でも違うものとして扱い、人の一覧の行を、毎回差し替えてしまう。
    /// </remarks>
    public bool Equals(ConversationSummary? other)
        => other is not null
        && Id == other.Id && Kind == other.Kind && Name == other.Name && PeerUserId == other.PeerUserId && PeerCanReceive == other.PeerCanReceive
        && LastMessageId == other.LastMessageId && LastText == other.LastText && LastAt == other.LastAt
        && LastSenderUserId == other.LastSenderUserId && LastSenderName == other.LastSenderName && UnreadCount == other.UnreadCount
        && (Readers is { } mine && other.Readers is { } theirs ? mine.SequenceEqual(theirs) : ReferenceEquals(Readers, other.Readers));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Kind, Name, LastMessageId, LastAt, UnreadCount, Readers?.Count ?? 0);
}
