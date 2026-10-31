namespace MmmTool.Messages.Core;

/// <summary>
/// メッセージの送り先。3 通りのどれか 1 つ。
/// </summary>
/// <param name="ConversationId">既にある会話の番号。0 のときは、ほかの 2 通り</param>
/// <param name="PeerUserId">1 対 1 の相手の番号。自分の番号なら、自分に送る。0 のときは、ほかの 2 通り</param>
/// <param name="GroupName">新しいグループの名前。新しいグループでないときは空文字</param>
/// <param name="GroupMemberIds">新しいグループの参加者の番号。自分は含めない。新しいグループでないときは空</param>
/// <remarks>
/// 会話は、最初の送信で作る。メッセージの無い会話を、DB に残さないため。
/// 作るときは、会話・参加者・メッセージ・通知の状態を、1 つのトランザクションで書く。
/// </remarks>
public sealed record MessageTarget(int ConversationId, int PeerUserId, string GroupName, IReadOnlyList<int> GroupMemberIds)
{
    /// <summary>新しいグループか</summary>
    public bool IsNewGroup => GroupName.Length > 0;

    /// <summary>既にある会話への送り先を作る</summary>
    /// <param name="conversationId">会話の番号</param>
    /// <returns>既にある会話への送り先</returns>
    public static MessageTarget Existing(int conversationId) => new(conversationId, 0, "", []);

    /// <summary>1 対 1 の相手への送り先を作る</summary>
    /// <param name="peerUserId">相手の番号。自分の番号なら、自分に送る</param>
    /// <returns>1 対 1 の相手への送り先。会話が無ければ、送るときに作る</returns>
    public static MessageTarget Direct(int peerUserId) => new(0, peerUserId, "", []);

    /// <summary>新しいグループへの送り先を作る</summary>
    /// <param name="name">グループの名前</param>
    /// <param name="memberIds">参加者の番号。自分は含めない</param>
    /// <returns>新しいグループへの送り先。送るときに、グループを作る</returns>
    public static MessageTarget NewGroup(string name, IReadOnlyList<int> memberIds)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(memberIds);

        return new MessageTarget(0, 0, name.Trim(), memberIds);
    }
}
