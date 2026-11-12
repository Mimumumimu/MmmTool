using MmmTool.NotificationChannels.Core;

namespace MmmBatch.MessageSending.Core;

/// <summary>送るメッセージ 1 件</summary>
/// <param name="Request">通知の状態</param>
/// <param name="Channel">
/// 送る先
/// 送る先の値を含む
/// 通知先を指定していないときは null
/// 送信先が削除されていることもある
/// </param>
/// <param name="OwnerName">
/// 通知を受ける人の表示名
/// 分からなければ空文字
/// </param>
/// <param name="SenderName">
/// メッセージを送った人の表示名
/// 分からなければ空文字
/// </param>
/// <param name="GroupName">
/// グループの会話の名前
/// 1 対 1 は空文字
/// 通知の見出しに入れる
/// </param>
/// <remarks>通知の状態と通知を受ける人の通知先</remarks>
public sealed record MessageSendTarget(MessageDeliveryItem Request, NotificationChannel? Channel, string OwnerName, string SenderName, string GroupName);
