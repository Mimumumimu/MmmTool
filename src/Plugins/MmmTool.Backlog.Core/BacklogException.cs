namespace MmmTool.Backlog.Core;

/// <summary>Backlog との通信に失敗した</summary>
/// <param name="message">画面に出せるメッセージ</param>
/// <param name="innerException">
/// 原因の例外
/// 無ければ null
/// </param>
/// <remarks>
/// メッセージはそのまま画面に出せる
/// API キーを含む URL は入れない
/// </remarks>
public sealed class BacklogException(string message, Exception? innerException = null) : Exception(message, innerException);
