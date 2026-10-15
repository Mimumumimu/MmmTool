using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmBatch.MessageSending.Core;

/// <summary>
/// 送るメッセージ 1 件 (送信依頼と、その送信先)。
/// </summary>
/// <param name="Request">送信依頼</param>
/// <param name="Channel">送る先 (送る先の値を含む。削除されていることもある)</param>
/// <param name="OwnerName">送信先を登録した人の表示名。分からなければ空文字</param>
/// <param name="SenderName">メッセージを依頼した人の表示名。分からなければ空文字</param>
public sealed record MessageSendTarget(MessageSendRequest Request, NotificationChannel Channel, string OwnerName, string SenderName);
