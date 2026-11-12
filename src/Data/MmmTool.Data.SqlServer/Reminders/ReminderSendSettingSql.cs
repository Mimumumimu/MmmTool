using System.Data;
using Microsoft.Data.SqlClient;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>dbo.ReminderSendSetting に送信先を書く SQL とパラメーターの作り方</summary>
/// <remarks>
/// リマインダー本体を書くトランザクションの中で、送信設定も一緒に書くために使う。
/// PC 版 (<see cref="SqlServerReminderRepository"/>)と Web 版 (<see cref="SqlServerReminderWebRepository"/>)の保存先が共有する。
/// 本体と送信設定のどちらかだけが書かれた状態を作らないため、トランザクションは呼ぶ側が持つ。
/// </remarks>
internal static class ReminderSendSettingSql
{
    /// <summary>リマインダーの送信先を選んだ一覧で置き換える</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="reminderIds">対象のリマインダーの番号</param>
    /// <param name="channelIds">
    /// 選んだ送信先の番号。
    /// 空なら、自分が使用者の送信先の設定をすべて外す。
    /// </param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <remarks>
    /// 送信先の確認と、不要な設定の論理削除と、選んだ設定の更新と、無いものの追加を、1 回の往復で行う。
    /// 番号は JSON の 1 つのパラメーターで渡す。
    /// リマインダーや送信先の数が増えても、問い合わせの数は変わらない。
    /// すでにあるものも含めて、選んだ設定の更新日時を今にする。
    /// 保存した時点で過ぎている今日の分を、MmmBatch が送らないため。
    /// 対象は自分が使用者の送信先だけで、ほかの人の送信先の設定は足さず、外さない。
    /// </remarks>
    public static async Task SetChannelsAsync(
        SqlConnection connection, SqlTransaction transaction, int userId,
        IReadOnlyCollection<int> reminderIds, IReadOnlyCollection<int> channelIds, CancellationToken cancellationToken)
    {
        var reminders = reminderIds.Where(id => id > 0).Distinct().ToList();
        if (reminders.Count == 0)
        {
            return;
        }

        var wanted = channelIds.Where(id => id > 0).Distinct().ToList();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            -- 自分が使用者の送信先だけを対象にする。ほかの人の送信先の番号が混ざっても、足さない
            DECLARE @Wanted TABLE (ChannelId int PRIMARY KEY);
            INSERT INTO @Wanted (ChannelId)
            SELECT DISTINCT j.ChannelId FROM OPENJSON(@Channels) WITH (ChannelId int '$') AS j
            WHERE EXISTS (SELECT 1 FROM dbo.NotificationChannel AS c WHERE c.Id = j.ChannelId AND c.UserId = @UserId AND c.IsDeleted = 0);

            DECLARE @Reminders TABLE (ReminderId int PRIMARY KEY);
            INSERT INTO @Reminders (ReminderId)
            SELECT DISTINCT j.ReminderId FROM OPENJSON(@ReminderIds) WITH (ReminderId int '$') AS j;

            -- 選んでいない送信先の設定を、論理削除する。自分が使用者の送信先の分だけ
            UPDATE s SET s.IsDeleted = 1, s.UpdatedAt = SYSDATETIMEOFFSET(), s.UpdatedByUserId = @UserId
            FROM dbo.ReminderSendSetting AS s
            WHERE s.ReminderId IN (SELECT ReminderId FROM @Reminders) AND s.IsDeleted = 0
              AND s.ChannelId NOT IN (SELECT ChannelId FROM @Wanted)
              AND s.ChannelId IN (SELECT Id FROM dbo.NotificationChannel WHERE UserId = @UserId);

            -- 選んだ送信先の既存の行 (削除済みも含む)を、更新日時を今にして、削除されていない行にする。削除されていない行を先にして、番号の大きい行を使う
            WITH ranked AS (
                SELECT s.IsDeleted, s.UpdatedAt, s.UpdatedByUserId,
                       ROW_NUMBER() OVER (PARTITION BY s.ReminderId, s.ChannelId ORDER BY s.IsDeleted, s.Id DESC) AS rn
                FROM dbo.ReminderSendSetting AS s
                WHERE s.ReminderId IN (SELECT ReminderId FROM @Reminders) AND s.ChannelId IN (SELECT ChannelId FROM @Wanted))
            UPDATE ranked SET IsDeleted = 0, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId WHERE rn = 1;

            -- 行が無いものだけ、追加する
            INSERT INTO dbo.ReminderSendSetting (CreatedByUserId, UpdatedByUserId, ReminderId, ChannelId)
            SELECT @UserId, @UserId, r.ReminderId, w.ChannelId
            FROM @Reminders AS r CROSS JOIN @Wanted AS w
            WHERE NOT EXISTS (SELECT 1 FROM dbo.ReminderSendSetting AS s WHERE s.ReminderId = r.ReminderId AND s.ChannelId = w.ChannelId);
            """;
        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
        command.Parameters.Add(new SqlParameter("@ReminderIds", SqlDbType.NVarChar, -1) { Value = IdJson.From(reminders) });
        command.Parameters.Add(new SqlParameter("@Channels", SqlDbType.NVarChar, -1) { Value = IdJson.From(wanted) });
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
