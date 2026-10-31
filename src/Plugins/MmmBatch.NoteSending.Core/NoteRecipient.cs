using MmmTool.NotificationChannels.Core;

namespace MmmBatch.NoteSending.Core;

/// <summary>
/// ノートの作成・更新を通知する相手 1 人と、その受け取り先。
/// </summary>
/// <param name="UserId">通知を受ける人 (<c>AppUser.Id</c>)</param>
/// <param name="Name">通知を受ける人の表示名。引けなかったときは空文字</param>
/// <param name="Channel">受け取り先の送信先。送る先の値を含む (秘密なので、画面・ログに出さない)</param>
public sealed record NoteRecipient(int UserId, string Name, NotificationChannel Channel);
