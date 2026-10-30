namespace MmmToolWeb.Messages.Client;

/// <summary>
/// 会話の中の 1 通。
/// </summary>
/// <param name="Id">メッセージの番号。</param>
/// <param name="ConversationId">会話の番号。</param>
/// <param name="SenderUserId">送った人の番号。</param>
/// <param name="SenderName">送った人の表示名。</param>
/// <param name="IsMine">自分が送ったメッセージか。サーバーが、自分の番号と比べて決める。</param>
/// <param name="ReplyToMessageId">返信元の番号。返信でなければ 0。</param>
/// <param name="ReplyToSenderName">返信元を送った人の表示名。返信でない、または読めないときは null。</param>
/// <param name="ReplyToText">返信元の本文の 1 行目。返信でない、または読めないときは null。</param>
/// <param name="Body">本文。</param>
/// <param name="CreatedAt">送った日時。並びの決めと、差分の取り合わせには使わず、時刻の表示の元にする。</param>
/// <param name="TimeText">送った日時の表示の文字。サーバーの時計で作る。</param>
/// <param name="DeliveryStatus">自分が送ったメッセージの、通知の状態。送信待ちは 1、送信済みは 2、失敗は 3。自分が送ったのでなければ 0。</param>
/// <param name="UpdatedAt">最後に更新した日時。同じメッセージが何度届いても、結果が変わらないように、突き合わせに使う。</param>
public sealed record MessageDto(
    int Id, int ConversationId, int SenderUserId, string SenderName, bool IsMine, int ReplyToMessageId, string? ReplyToSenderName, string? ReplyToText,
    string Body, DateTimeOffset CreatedAt, string TimeText, int DeliveryStatus, DateTimeOffset UpdatedAt);

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
public sealed record ConversationDto(int Id, bool IsDirect, string Name, int PeerUserId, bool PeerCanReceive, int LastMessageId, string LastText, string TimeText);

/// <summary>
/// 会話とメッセージの読み込みの結果。最初の読み込みと、差分の取得で共通。
/// </summary>
/// <param name="UserId">自分の番号。端末の控えを、人ごとに分けるために使う。</param>
/// <param name="NextBase">次の差分の取得に送る基準の日時。サーバーが返した値を、そのまま送り返す。</param>
/// <param name="Conversations">変更のあった会話の要約。最初の読み込みでは、自分の会話すべて。</param>
/// <param name="Messages">変更のあったメッセージ。最初の読み込みでは空。</param>
public sealed record MessageSyncDto(int UserId, DateTimeOffset NextBase, IReadOnlyList<ConversationDto> Conversations, IReadOnlyList<MessageDto> Messages);

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
public sealed record MessageSendDto(int ConversationId, int PeerUserId, string Body, int ReplyToMessageId);

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
public sealed record MessageFormDto(int BodyMaxLength, bool SelfCanReceive, bool PeerCanReceive);

