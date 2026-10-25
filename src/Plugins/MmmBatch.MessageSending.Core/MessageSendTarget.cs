using MmmTool.NotificationChannels.Core;

namespace MmmBatch.MessageSending.Core;

/// <summary>
/// 送るメッセージ 1 件。通知の状態と、通知を受ける人の受け取り先。
/// </summary>
/// <param name="Request">通知の状態</param>
/// <param name="Channel">送る先。送る先の値を含む。受け取り先を指定していないときは null。送信先が削除されていることもある</param>
/// <param name="OwnerName">通知を受ける人の表示名。分からなければ空文字</param>
/// <param name="SenderName">メッセージを送った人の表示名。分からなければ空文字</param>
public sealed record MessageSendTarget(MessageDeliveryItem Request, NotificationChannel? Channel, string OwnerName, string SenderName);
