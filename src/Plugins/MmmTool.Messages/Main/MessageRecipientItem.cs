using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Messages.Main;

/// <summary>宛先の選択肢 1 件 (画面に出す名前)</summary>
/// <param name="source">元の宛先</param>
/// <remarks>送る先の値 (トピック名・Webhook の URL)は持たない (秘密のため)。</remarks>
public sealed class MessageRecipientItem(MessageRecipient source)
{
    /// <summary>元の宛先</summary>
    public MessageRecipient Source { get; } = source;

    /// <summary>送信先の番号 (<c>NotificationChannel.Id</c>)</summary>
    public int ChannelId => Source.ChannelId;

    /// <summary>画面に出す名前 (「ユーザー名 - 登録名 (ntfy)」「ユーザー名 - 登録名 (Discord)」の形)</summary>
    public string DisplayName { get; } = $"{source.UserName} - {source.ChannelName} ({KindText(source.Kind)})";

    /// <summary>送信先の区分を、画面に出す名前にする</summary>
    /// <param name="kind">送信先の区分</param>
    /// <returns>ntfy は「ntfy」、Discord は「Discord」</returns>
    private static string KindText(NotificationChannelKind kind) => kind switch
    {
        NotificationChannelKind.Ntfy => "ntfy",
        NotificationChannelKind.Discord => "Discord",
        _ => kind.ToString(),
    };
}
