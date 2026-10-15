using MmmTool.NotificationChannels.Core;

namespace MmmTool.Messages.Core;

/// <summary>
/// メッセージの宛先 1 件 (送信先と、その使用者)。
/// </summary>
/// <param name="ChannelId">送信先の番号 (<c>NotificationChannel.Id</c>)</param>
/// <param name="UserName">送信先を使う人の表示名</param>
/// <param name="ChannelName">送信先の登録名</param>
/// <param name="Kind">送信先の区分</param>
/// <remarks>送る先の値 (トピック名・Webhook の URL)は秘密なので、持たない。</remarks>
public sealed record MessageRecipient(int ChannelId, string UserName, string ChannelName, NotificationChannelKind Kind);
