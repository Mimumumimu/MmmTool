using MmmTool.Reminders.Core;

namespace MmmTool.Reminders;

/// <summary>送信先の区分 (<see cref="NotificationChannelKind"/>)の、画面に出す名前</summary>
/// <remarks>一覧の行と、登録画面の「種類」の選択肢で、同じ名前にそろえる。</remarks>
public static class NotificationChannelKinds
{
    /// <summary>区分を、画面に出す名前にする</summary>
    /// <param name="kind">区分</param>
    /// <returns>画面に出す名前 (「ntfy」「Discord」)</returns>
    public static string ToText(NotificationChannelKind kind) => kind switch
    {
        NotificationChannelKind.Ntfy => "ntfy",
        NotificationChannelKind.Discord => "Discord",
        _ => kind.ToString(),
    };
}
