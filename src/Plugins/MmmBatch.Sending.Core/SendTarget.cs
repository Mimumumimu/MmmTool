using MmmTool.Reminders.Core;

namespace MmmBatch.Sending.Core;

/// <summary>
/// 送る対象 1 件 (リマインダーと、その送信先)。
/// </summary>
/// <param name="Reminder">送るリマインダー</param>
/// <param name="Channel">送る先</param>
/// <param name="SettingUpdatedAt">送信設定を最後に更新した日時 (DB の時計)</param>
/// <param name="OwnerName">送信先を登録した人の表示名。分からなければ空文字</param>
/// <remarks>送信設定を、今日の発動時刻より後に付けた (更新した)ときは、今日の分は送らない (付けた時点で過ぎている分を、すぐ送らないため)。</remarks>
public sealed record SendTarget(Reminder Reminder, NotificationChannel Channel, DateTimeOffset SettingUpdatedAt, string OwnerName);
