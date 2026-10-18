namespace MmmTool.Messages.Core;

/// <summary>メッセージの下書きの決まり (MmmTool の画面と Web 版で共有する)</summary>
public static class MessageDraft
{
    /// <summary>選んだ送信先が、選んだあとに削除されて無いときの理由</summary>
    public const string RecipientMissingMessage = "選んだ送信先がありません。削除された可能性があります。宛先を選び直してください。";

    /// <summary>本文の最初の入力 (改行 2 つと「○○より」)を作る</summary>
    /// <param name="displayName">今のユーザーの表示名。分からなければ null か空文字</param>
    /// <returns>署名つきの最初の入力。表示名が分からなければ空文字</returns>
    /// <remarks>送る人が何を送るか見えるよう、本文欄に実際に入れ、送るときには足さない。書き換えても消してもよい。</remarks>
    public static string CreateSignature(string? displayName)
        => string.IsNullOrEmpty(displayName) ? "" : $"\n\n{displayName}より";

    /// <summary>送れる内容か</summary>
    /// <param name="hasRecipient">宛先を選んでいるか</param>
    /// <param name="body">本文 (署名を含む)</param>
    /// <param name="signature">本文の最初の入力 (<see cref="CreateSignature"/>)</param>
    /// <returns>宛先があり、署名以外の本文があるとき true</returns>
    public static bool CanSend(bool hasRecipient, string body, string signature)
        => hasRecipient && !string.IsNullOrWhiteSpace(body) && body.Trim() != signature.Trim();
}
