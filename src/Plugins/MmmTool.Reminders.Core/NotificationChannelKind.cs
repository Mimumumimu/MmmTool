namespace MmmTool.Reminders.Core;

/// <summary>送信先 (<see cref="NotificationChannel"/>)の区分</summary>
/// <remarks>DB には数値で入る (<c>dbo.NotificationChannel.Kind</c>)。値を変えると、保存済みの内容の意味が変わる。</remarks>
public enum NotificationChannelKind
{
    /// <summary>ntfy。値はトピック名</summary>
    Ntfy = 1,

    /// <summary>Discord。値は Webhook の URL</summary>
    Discord = 2,
}
