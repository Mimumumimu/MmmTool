namespace MmmTool.Messages.Core;

/// <summary>メッセージの下書きの決まり。MmmTool の画面と Web 版のサーバーで共有する</summary>
public static class MessageDraft
{
    /// <summary>相手が受け取り先を指定していないときの理由</summary>
    public const string NoReceiveTargetMessage = "受け取り先が未設定です";

    /// <summary>送る人が、自分の受け取り先を指定していないときの理由</summary>
    public const string SelfNoReceiveTargetMessage = "自分の受け取り先が未設定です。設定で指定すると送れます";

    /// <summary>送れる内容か</summary>
    /// <param name="selfCanReceive">送る人が、自分の受け取り先を指定しているか</param>
    /// <param name="peerCanReceive">相手が、受け取り先を指定しているか</param>
    /// <param name="body">本文</param>
    /// <returns>送る人と相手の両方が受け取り先を指定していて、本文が空白だけでないとき true</returns>
    /// <remarks>
    /// 送る人も受け取り先を指定していないと送れない。返事を受け取れない人が、一方的に送る形にしないため。
    /// 送った人は、本文に書かせない。送るとき、システムが知っている送った人を、MmmBatch が通知に付ける。
    /// </remarks>
    public static bool CanSend(bool selfCanReceive, bool peerCanReceive, string body)
        => selfCanReceive && peerCanReceive && !string.IsNullOrWhiteSpace(body);

    /// <summary>グループの名前が、使える長さか調べる</summary>
    /// <param name="name">名前。前後の空白を取る前の文字</param>
    /// <returns>前後の空白を取った名前が、空でなく、上限以内のとき true</returns>
    public static bool IsValidGroupName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        return trimmed.Length > 0 && trimmed.Length <= MessageLimits.GroupNameMaxLength;
    }

    /// <summary>新しいグループに、参加者として選べる人数か調べる</summary>
    /// <param name="otherMemberCount">自分のほかの参加者の人数</param>
    /// <returns>1 人以上で、自分を含めて上限以内のとき true</returns>
    public static bool IsValidGroupMemberCount(int otherMemberCount)
        => otherMemberCount >= 1 && otherMemberCount + 1 <= MessageLimits.GroupMaxMembers;

    /// <summary>本文が、送れる長さか調べる</summary>
    /// <param name="body">本文。前後の空白を取る前の文字</param>
    /// <returns>前後の空白を取った本文が、空でなく、上限以内のとき true</returns>
    public static bool IsValidBody(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var trimmed = body.Trim();
        return trimmed.Length > 0 && trimmed.Length <= MessageLimits.BodyMaxLength;
    }
}
