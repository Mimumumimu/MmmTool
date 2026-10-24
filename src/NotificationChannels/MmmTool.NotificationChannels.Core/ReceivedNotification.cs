namespace MmmTool.NotificationChannels.Core;

/// <summary>
/// 受け取った通知 1 件。自分の送信先に届いた、送信済みの通知。
/// </summary>
/// <param name="Id">元の行の番号。種類ごとに、種類の中で一意</param>
/// <param name="Title">見出し。メッセージは送った人とタイトル、リマインダーは件名</param>
/// <param name="Body">本文。メッセージは本文、リマインダーは備考。無ければ空文字</param>
/// <param name="Link">リンク。無ければ null</param>
/// <param name="ReceivedAt">送った日時。届いた日時として並べる</param>
public sealed record ReceivedNotification(int Id, string Title, string Body, string? Link, DateTimeOffset ReceivedAt);
