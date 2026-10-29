namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// 端末の宛先から探して見つけた、自分の送信先 1 件。
/// </summary>
/// <param name="Id">番号 (<c>dbo.NotificationChannel.Id</c>)</param>
/// <param name="IsDeleted">論理削除されているか</param>
/// <remarks>送る先の値を読めない DB のログイン (Web 版)でも、宛先がどの行かを知るために使う。値そのものは持たない。</remarks>
public sealed record NotificationChannelMatch(int Id, bool IsDeleted);
