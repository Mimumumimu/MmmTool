namespace MmmBatch.Sending.Core;

/// <summary>外部へ送れなかった</summary>
/// <remarks>
/// メッセージは画面に出せる形にする。
/// 送る先の値 (トピック名・Webhook の URL)は秘密なので、メッセージに含めない。
/// </remarks>
/// <param name="message">画面に出せるメッセージ</param>
/// <param name="innerException">
/// 原因の例外
/// 無ければ null
/// </param>
public sealed class SendFailedException(string message, Exception? innerException = null) : Exception(message, innerException);
