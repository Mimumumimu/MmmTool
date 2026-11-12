namespace MmmTool.NotificationChannels.Core;

/// <summary>送信先の区分</summary>
/// <remarks>
/// <see cref="NotificationChannel"/>
/// DB には数値で入る
/// 列は <c>dbo.NotificationChannel.Kind</c>
/// 値を変えると保存済みの内容の意味が変わる
/// </remarks>
public enum NotificationChannelKind
{
    /// <summary>ntfy</summary>
    /// <remarks>値はトピック名</remarks>
    Ntfy = 1,

    /// <summary>Discord</summary>
    /// <remarks>値は Webhook の URL</remarks>
    Discord = 2,

    /// <summary>Web 版通知</summary>
    /// <remarks>
    /// <see cref="WebPushSubscription"/>の JSON
    /// 値はブラウザが作った通知の宛先
    /// </remarks>
    WebPush = 3,
}
