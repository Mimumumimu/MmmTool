using System.Data;
using Microsoft.Data.SqlClient;
using MmmBatch.ReminderSending.Core;
using MmmBatch.Sending.Core;
using MmmSdk.Core.Components.DaysOfWeek;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Users;

namespace MmmBatch.Data.SqlServer.Sending;

/// <summary>送る対象を SQL Server から読む</summary>
/// <remarks>
/// 送信先を選んだリマインダーとその送信先。
/// 読むだけで、書かない。
/// リマインダーの宛先・作成者・対応状態は見ない。
/// 送信は登録した人が選んだ送信先へ送るため。
/// 削除されたリマインダー・送信先・送信設定は含めない。
/// 読み方は全件と新しく送るものと送り直すものの 3 つ。
/// 列と結合と条件は共通にして、取り出す行の絞り込みだけを変える。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerReminderSendTargetSource(SqlServerDatabase database) : IReminderSendTargetSource
{
    /// <summary>分の終わりまで</summary>
    /// <remarks>秒が 0 の範囲の時刻に足して、その分の 59 秒までを含める。</remarks>
    private static readonly TimeSpan EndOfMinute = TimeSpan.FromSeconds(59);

    /// <summary>読む列</summary>
    /// <remarks>
    /// 並びは <see cref="ReadTargets"/> と合わせる
    /// 末尾は今日の送信の状況と昨日の送信の状況
    /// </remarks>
    private static readonly string Columns =
        $"""
        SELECT r.Id, r.IsDeleted, r.AuthorUserId, r.TargetUserId, r.Date, r.Time, r.Weekdays, r.Title, r.Note, r.Link, r.IsSpeak,
               c.Id, c.IsDeleted, c.UserId, c.Kind, c.Name, c.Value, s.UpdatedAt, ISNULL(u.DisplayName, N''),
               {SqlServerReminderSendStatusRepository.ColumnsOf("t")},
               {SqlServerReminderSendStatusRepository.ColumnsOf("y")}
        """;

    /// <summary>送信設定から削除されていないリマインダーを引く結合</summary>
    /// <remarks>前に <c>dbo.ReminderSendSetting AS s</c> が要る</remarks>
    private const string ReminderJoin = "INNER JOIN dbo.Reminder AS r ON r.Id = s.ReminderId AND r.IsDeleted = 0";

    /// <summary>送信先・持ち主・今日と昨日の送信の状況を結ぶ</summary>
    /// <remarks>
    /// 状況の表は <c>(ReminderId, ChannelId, Date)</c> が一意なので、結合しても対象の行は増えない。
    /// 前に送信設定 <c>s</c> とリマインダー <c>r</c> が要る。
    /// </remarks>
    private const string Joins =
        """
        INNER JOIN dbo.NotificationChannel AS c ON c.Id = s.ChannelId AND c.IsDeleted = 0
        LEFT JOIN dbo.AppUser AS u ON u.Id = c.UserId
        LEFT JOIN dbo.ReminderSendStatus AS t ON t.ReminderId = s.ReminderId AND t.ChannelId = s.ChannelId AND t.Date = @Today
        LEFT JOIN dbo.ReminderSendStatus AS y ON y.ReminderId = s.ReminderId AND y.ChannelId = s.ChannelId AND y.Date = @Yesterday
        """;

    /// <summary>3 つの読み方に共通の条件</summary>
    /// <remarks>
    /// 管理者にリマインダーを無効にされた持ち主の送信先へは送らない。
    /// 宛先が全員でも送信先の持ち主でもないときは送らない。
    /// 宛先ではない人の送信先へ、通知が届かないようにするため。
    /// </remarks>
    private static readonly string Condition =
        $"""
        s.IsDeleted = 0 AND (r.TargetUserId = 0 OR r.TargetUserId = c.UserId)
          AND NOT {FeatureAccessSql.IsDisabled("c.UserId", CodeFeatures.Reminders)}
        """;

    /// <summary>リマインダーが今日発動する条件</summary>
    /// <remarks>
    /// 発動日が今日か、曜日指定 (<c>9999-12-31</c>)で今日の曜日を含む。
    /// 曜日が 0 か 7 つ全部なら毎日。
    /// 先頭の <c>IN</c> は日付の索引で範囲を絞るためで、意味は後ろの条件と同じ。
    /// <c>@DayBit</c> は今日の曜日の値で、<see cref="ReadAsync"/> が足す。
    /// </remarks>
    private const string OccursToday =
        "r.Date IN (@Today, '9999-12-31') AND (r.Date = @Today OR r.Weekdays IN (0, 127) OR (r.Weekdays & @DayBit) <> 0)";

    /// <inheritdoc />
    /// <remarks>
    /// 画面に今日の送信予定を出すための読み方
    /// 今日発動するものに絞る
    /// 時刻は絞らない
    /// </remarks>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// 設定が足りない・接続できない・表が無い・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<IReadOnlyList<ReminderSendTargetState>> GetTargetsAsync(DateOnly today, CancellationToken cancellationToken = default)
        => ReadAsync(
            $"""
            {Columns}
            FROM dbo.ReminderSendSetting AS s
            {ReminderJoin}
            {Joins}
            WHERE {OccursToday} AND {Condition}
            ORDER BY r.Time, r.Id
            """,
            today,
            command => { },
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 日付と曜日は <see cref="OccursToday"/>。
    /// 時刻は <c>(@After, @Through]</c> で、<c>@After</c> が無ければ 0:00 を含めて読む。
    /// 範囲は分の単位で、終わりの分の 59 秒までを含む。
    /// DB の時刻は秒を持てるため。
    /// 削除されていない行だけの索引 <c>IX_Reminder_Date_Time_Weekdays</c> で、日付と時刻の範囲を探す。
    /// 今日の送信の状況がある対象は読まない。
    /// その送り直しは <see cref="GetRetryTargetsAsync"/> が読む。
    /// </remarks>
    public Task<IReadOnlyList<ReminderSendTargetState>> GetDueTargetsAsync(DateOnly today, TimeOnly? after, TimeOnly through, CancellationToken cancellationToken = default)
        => ReadAsync(
            $"""
            {Columns}
            FROM dbo.Reminder AS r
            INNER JOIN dbo.ReminderSendSetting AS s ON s.ReminderId = r.Id
            {Joins}
            WHERE r.IsDeleted = 0
              AND r.Time <= @Through {(after is null ? "" : "AND r.Time > @After")}
              AND {OccursToday}
              AND t.Id IS NULL
              AND {Condition}
            ORDER BY r.Time, r.Id
            """,
            today,
            command =>
            {
                command.Parameters.Add(new SqlParameter("@Through", SqlDbType.Time) { Scale = 0, Value = through.ToTimeSpan() + EndOfMinute });
                if (after is { } afterTime)
                {
                    command.Parameters.Add(new SqlParameter("@After", SqlDbType.Time) { Scale = 0, Value = afterTime.ToTimeSpan() + EndOfMinute });
                }
            },
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// 今日と昨日の状況のうち、送り終えておらず、回数が上限に満たないものを、索引 <c>IX_ReminderSendStatus_Date_Status_Attempts</c> で探す。
    /// その組 (リマインダー × 送信先)に送信設定がまだあるものだけを、同じ結合で読む。
    /// </remarks>
    public Task<IReadOnlyList<ReminderSendTargetState>> GetRetryTargetsAsync(DateOnly today, CancellationToken cancellationToken = default)
        => ReadAsync(
            $"""
            {Columns}
            FROM (
                SELECT ReminderId, ChannelId
                FROM dbo.ReminderSendStatus
                WHERE Date IN (@Today, @Yesterday) AND Status IN (@Pending, @Failed) AND Attempts < @MaxAttempts
                GROUP BY ReminderId, ChannelId
            ) AS k
            INNER JOIN dbo.ReminderSendSetting AS s ON s.ReminderId = k.ReminderId AND s.ChannelId = k.ChannelId
            {ReminderJoin}
            {Joins}
            WHERE {Condition}
            ORDER BY r.Id
            """,
            today,
            command =>
            {
                command.Parameters.Add(new SqlParameter("@Pending", SqlDbType.TinyInt) { Value = (byte)SendStatus.Pending });
                command.Parameters.Add(new SqlParameter("@Failed", SqlDbType.TinyInt) { Value = (byte)SendStatus.Failed });
                command.Parameters.Add(new SqlParameter("@MaxAttempts", SqlDbType.Int) { Value = SendRetryPolicy.MaxAttempts });
            },
            cancellationToken);

    /// <summary>問い合わせを流して送る対象を読む</summary>
    /// <param name="commandText">問い合わせの文で <c>@Today</c> と <c>@Yesterday</c> を使える</param>
    /// <param name="today">今日</param>
    /// <param name="addParameters">問い合わせごとのパラメーターを足す処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読んだ送る対象</returns>
    private Task<IReadOnlyList<ReminderSendTargetState>> ReadAsync(
        string commandText, DateOnly today, Action<SqlCommand> addParameters, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync<IReadOnlyList<ReminderSendTargetState>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@Today", SqlDbType.Date) { Value = today.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@Yesterday", SqlDbType.Date) { Value = today.AddDays(-1).ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@DayBit", SqlDbType.TinyInt) { Value = (byte)WeekdaySet.From(today.DayOfWeek) });
            addParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await ReadTargets(reader, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>読み出した行を送る対象にする</summary>
    /// <param name="reader"><see cref="Columns"/> の並びで読む読み取り口</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送る対象</returns>
    private static async Task<IReadOnlyList<ReminderSendTargetState>> ReadTargets(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var targets = new List<ReminderSendTargetState>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var reminder = new ReminderRow
            {
                Id = reader.GetInt32(0),
                IsDeleted = reader.GetBoolean(1),
                AuthorUserId = reader.GetInt32(2),
                TargetUserId = reader.GetInt32(3),
                Date = DateOnly.FromDateTime(reader.GetDateTime(4)),
                Time = TimeOnly.FromTimeSpan(reader.GetTimeSpan(5)),
                Weekdays = reader.GetByte(6),
                Title = reader.GetString(7),
                Note = reader.GetString(8),
                Link = reader.GetString(9),
                IsSpeak = reader.GetBoolean(10),
            }.ToReminder();
            var channel = new NotificationChannelRow
            {
                Id = reader.GetInt32(11),
                IsDeleted = reader.GetBoolean(12),
                UserId = reader.GetInt32(13),
                Kind = reader.GetByte(14),
                Name = reader.GetString(15),
                Value = reader.GetString(16),
            }.ToChannel();
            targets.Add(new ReminderSendTargetState(
                new ReminderSendTarget(reminder, channel, reader.GetDateTimeOffset(17), reader.GetString(18)),
                SqlServerReminderSendStatusRepository.ReadStatusOrNull(reader, 19),
                SqlServerReminderSendStatusRepository.ReadStatusOrNull(reader, 27)));
        }
        return targets;
    }
}
