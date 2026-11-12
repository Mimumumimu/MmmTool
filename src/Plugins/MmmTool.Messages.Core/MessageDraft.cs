namespace MmmTool.Messages.Core;

/// <summary>メッセージの下書きの決まり</summary>
/// <remarks>MmmTool の画面と Web 版のサーバーで共有する</remarks>
public static class MessageDraft
{
    /// <summary>1 対 1 の相手が通知先を指定していないとき、入力欄の近くに出す案内</summary>
    /// <remarks>送れるが通知は届かない</remarks>
    public const string PeerNoNotifyMessage = "相手は Web 版通知が未設定のため、通知は届きません。";

    /// <summary>会話の一覧で、相手が通知先を指定していないときに出す短い案内</summary>
    public const string PeerNoNotifyListText = "Web 版通知が未設定です";

    /// <summary>送れる内容か</summary>
    /// <param name="body">本文</param>
    /// <returns>本文が空白だけでないとき true</returns>
    /// <remarks>
    /// 通知先を指定していない人にも送れる。
    /// 通知が届かないだけで、相手はアプリを開けば読める。
    /// 送った人は本文に書かせない。
    /// 送るとき、システムが知っている送った人を、MmmBatch が通知に付ける。
    /// </remarks>
    public static bool CanSend(string body)
        => !string.IsNullOrWhiteSpace(body);

    /// <summary>グループの名前が使える長さか調べる</summary>
    /// <param name="name">
    /// 名前
    /// 前後の空白を取る前の文字
    /// </param>
    /// <returns>前後の空白を取った名前が空でなく、上限以内のとき true。</returns>
    public static bool IsValidGroupName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        return trimmed.Length > 0 && trimmed.Length <= MessageLimits.GroupNameMaxLength;
    }

    /// <summary>新しいグループに参加者として選べる人数か調べる</summary>
    /// <param name="otherMemberCount">自分のほかの参加者の人数</param>
    /// <returns>1 人以上で、自分を含めて上限以内のとき true。</returns>
    public static bool IsValidGroupMemberCount(int otherMemberCount)
        => otherMemberCount >= 1 && otherMemberCount + 1 <= MessageLimits.GroupMaxMembers;

    /// <summary>本文が送れる長さか調べる</summary>
    /// <param name="body">
    /// 本文
    /// 前後の空白を取る前の文字
    /// </param>
    /// <returns>前後の空白を取った本文が空でなく、上限以内のとき true。</returns>
    public static bool IsValidBody(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var trimmed = body.Trim();
        return trimmed.Length > 0 && trimmed.Length <= MessageLimits.BodyMaxLength;
    }
}
