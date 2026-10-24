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
/// <remarks>最後のメッセージは、会話の表に持たず、メッセージの表から、会話ごとに最新の 1 通を読んで求める。</remarks>
public sealed record ConversationSummary(
    int Id, ConversationKind Kind, string Name, int PeerUserId, bool PeerCanReceive, int LastMessageId, string LastText, DateTimeOffset LastAt);
