namespace MmmTool.NotificationChannels.Core;

/// <summary>送信先の区分 (<see cref="NotificationChannelKind"/>)の、画面に出す名前</summary>
/// <remarks>一覧の行・登録画面の「種類」の選択肢・宛先の選択肢・送信の履歴で、同じ名前にそろえる。</remarks>
public static class NotificationChannelKinds
{
    /// <summary>区分を、画面に出す名前にする</summary>
    /// <param name="kind">区分</param>
    /// <returns>画面に出す名前 (「ntfy」「Discord」「Web 版通知」)</returns>
    public static string ToText(NotificationChannelKind kind) => kind switch
    {
        NotificationChannelKind.Ntfy => "ntfy",
        NotificationChannelKind.Discord => "Discord",
        NotificationChannelKind.WebPush => "Web 版通知",
        _ => kind.ToString(),
    };
}
