using MmmTool.Messages.Core;

namespace MmmTool.Messages.Main;

/// <summary>宛先の選択肢 1 件 (画面に出す名前)</summary>
/// <param name="source">元の宛先</param>
/// <remarks>送る先の値であるトピック名や Webhook の URL は、秘密なので持たない。</remarks>
public sealed class MessageRecipientItem(MessageRecipient source)
{
    /// <summary>元の宛先</summary>
    public MessageRecipient Source { get; } = source;

    /// <summary>送信先の番号 (<c>NotificationChannel.Id</c>)</summary>
    public int ChannelId => Source.ChannelId;

    /// <summary>画面に出す名前 (「ユーザー名 - 登録名 (ntfy)」「ユーザー名 - 登録名 (Discord)」の形)</summary>
    public string DisplayName => Source.DisplayName;
}
