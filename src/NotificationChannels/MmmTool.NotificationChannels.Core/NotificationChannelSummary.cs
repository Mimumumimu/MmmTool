namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// 送信先 (<see cref="NotificationChannel"/>)の、秘密の値を含まない要約。
/// </summary>
/// <param name="Id">番号 (<c>dbo.NotificationChannel.Id</c>)</param>
/// <param name="Name">登録名 (画面に出す名前)</param>
/// <remarks>送る先の値を読めない DB のログイン (Web 版)でも、登録済みの送信先を一覧にするために使う。</remarks>
public sealed record NotificationChannelSummary(int Id, string Name);
