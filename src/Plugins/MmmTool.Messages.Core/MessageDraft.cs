namespace MmmTool.Messages.Core;

/// <summary>メッセージの下書きの決まり。MmmTool の画面と Web 版のサーバーで共有する</summary>
public static class MessageDraft
{
    /// <summary>相手が受け取り先を指定していないときの理由</summary>
    public const string NoReceiveTargetMessage = "受け取り先が未設定です";

    /// <summary>送れる内容か</summary>
    /// <param name="peerCanReceive">相手が、受け取り先を指定しているか</param>
    /// <param name="body">本文</param>
    /// <returns>相手が受け取り先を指定していて、本文が空白だけでないとき true</returns>
    /// <remarks>送った人は、本文に書かせない。送るとき、システムが知っている送った人を、MmmBatch が通知に付ける。</remarks>
    public static bool CanSend(bool peerCanReceive, string body)
        => peerCanReceive && !string.IsNullOrWhiteSpace(body);

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
