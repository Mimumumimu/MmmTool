using System.Globalization;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.NotificationChannels.Core;

namespace MmmToolWeb.Reminders;

/// <summary>
/// リマインダーの通知を押したときに、そのリマインダーを強調して、リマインダーの画面を開く行き先。
/// </summary>
/// <remarks>
/// 「通知」の機能は、リマインダーの名前を持たない。この行き先を登録するのは、リマインダーの機能。
/// 通知の識別子の番号は、送信の状況 (<c>ReminderSendStatus.Id</c>)の番号。自分の送信先に届いた記録から、リマインダーの番号と送った日を、1 回の問い合わせで引く。
/// 記録が自分のものでないとき、リマインダーが見えない・削除済みのときは、強調せずに、リマインダーの画面を開く。
/// </remarks>
/// <param name="repository">リマインダーの保存先</param>
public sealed class ReminderNotificationDestination(SqlServerReminderWebRepository repository) : INotificationDestination
{
    /// <summary>リマインダーの画面の道。<c>RemindersPage.Route</c> と同じ</summary>
    private const string RemindersPath = "/reminders";

    /// <inheritdoc />
    public string Key => "reminder";

    /// <inheritdoc />
    public async Task<string?> GetPathAsync(int id, CancellationToken cancellationToken = default)
    {
        if (await repository.FindNotifiedAsync(id, cancellationToken) is not { } notified)
        {
            return RemindersPath;
        }

        return $"{RemindersPath}?no={notified.ReminderNo}&date={notified.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }
}
