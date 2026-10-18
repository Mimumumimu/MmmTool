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
public sealed record MessageRecipient(int ChannelId, string UserName, string ChannelName, NotificationChannelKind Kind)
{
    /// <summary>画面に出す名前 (「ユーザー名 - 登録名 (ntfy)」「ユーザー名 - 登録名 (Discord)」の形)</summary>
    public string DisplayName => $"{UserName} - {ChannelName} ({KindText(Kind)})";

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
