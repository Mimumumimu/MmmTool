using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.Reminders;
using MmmTool.Data.SqlServer.Users;
using MmmTool.Reminders.Core;

namespace MmmTool.Data.SqlServer.Reminders;

/// <summary>
/// <c>dbo.Reminder</c> を読み書きする SQL と、パラメーターの作り方。PC 版の保存先 (<see cref="SqlServerReminderRepository"/>)と、Web 版の保存先 (<see cref="SqlServerReminderWebRepository"/>)が共有する。
/// </summary>
/// <remarks>
/// 書き込みの SQL を 2 つの保存先に写さないため、1 か所に置く。見える範囲 (宛先が自分・全員宛て・自分が作成したもの)の条件も、ここで決める。
/// </remarks>
internal static class ReminderSql
{
    /// <summary>リマインダー本体の列。<see cref="ReadReminder"/> の並び</summary>
    public const string Columns = "Id, IsDeleted, AuthorUserId, TargetUserId, Date, Time, Weekdays, Title, Note, Link, IsSpeak";

    /// <summary>リマインダー本体の列を選ぶ問い合わせの先頭部分。<see cref="ReadReminder"/> の並び</summary>
    public const string Select = "SELECT " + Columns + " FROM dbo.Reminder";

    /// <summary>リマインダー本体の列に、表の別名を付けた並び。ほかの表と結合するときに使う</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>別名つきの列を、カンマでつないだ文字。並びは <see cref="Columns"/> と同じ</returns>
    public static string PrefixedColumns(string alias)
        => string.Join(", ", Columns.Split(", ").Select(column => $"{alias}.{column}"));

    /// <summary>見える範囲の条件。宛先が自分・全員宛て・自分が作成したもの。<c>@UserId</c> が要る</summary>
    public const string VisibleCondition = "(TargetUserId IN (0, @UserId) OR AuthorUserId = @UserId)";

    /// <summary>読んでいる行を、リマインダーにする</summary>
    /// <param name="reader">先頭の列が <see cref="Select"/> の並びで始まっているリーダー</param>
    /// <returns>リマインダー</returns>
    public static Reminder ReadReminder(SqlDataReader reader) => new ReminderRow
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

    /// <summary>リマインダーを追加する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="row">追加する行 (番号は無視する)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>DB が採番した番号</returns>
    /// <exception cref="DataFileException">今のユーザー、または宛先の人が、リマインダーを無効にされている。メッセージは画面に出せる。</exception>
    public static async Task<int> InsertAsync(
        SqlConnection connection, SqlTransaction transaction, int userId, ReminderRow row, CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(connection, transaction, userId, row.TargetUserId, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO dbo.Reminder
                (IsDeleted, CreatedByUserId, UpdatedByUserId, AuthorUserId, TargetUserId, Date, Time, Weekdays, Title, Note, Link, IsSpeak)
            OUTPUT INSERTED.Id
            VALUES
                (@IsDeleted, @UserId, @UserId, @UserId, @TargetUserId, @Date, @Time, @Weekdays, @Title, @Note, @Link, @IsSpeak)
            """;
        AddUserParameter(command, userId);
        AddReminderParameters(command, row);
        return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>リマインダーを更新する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="row">更新する行 (番号で対象を決める)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>見えて更新できたとき true。無い・見えないとき false</returns>
    /// <remarks>編集できるのは、宛先が自分・全員宛て・自分が作成したもので、見える人だけ。削除済みを編集すると、復活する。呼ぶ側が <c>IsDeleted</c> を false にして渡す。</remarks>
    /// <exception cref="DataFileException">今のユーザー、または宛先の人が、リマインダーを無効にされている。メッセージは画面に出せる。</exception>
    public static async Task<bool> UpdateAsync(
        SqlConnection connection, SqlTransaction transaction, int userId, ReminderRow row, CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(connection, transaction, userId, row.TargetUserId, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            UPDATE dbo.Reminder
            SET IsDeleted = @IsDeleted,
                TargetUserId = @TargetUserId, Date = @Date, Time = @Time, Weekdays = @Weekdays,
                Title = @Title, Note = @Note, Link = @Link, IsSpeak = @IsSpeak,
                UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
            WHERE Id = @Id AND {VisibleCondition}
            """;
        AddUserParameter(command, userId);
        AddReminderParameters(command, row);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = row.Id });
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <summary>リマインダーの削除フラグ (論理削除)を変える</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="no">対象の番号</param>
    /// <param name="isDeleted">削除済みにするか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>見えて書けたとき true。無い・見えないとき false</returns>
    public static async Task<bool> SetDeletedAsync(
        SqlConnection connection, SqlTransaction transaction, int userId, int no, bool isDeleted, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            UPDATE dbo.Reminder
            SET IsDeleted = @IsDeleted, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
            WHERE Id = @Id AND {VisibleCondition}
            """;
        AddUserParameter(command, userId);
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = no });
        command.Parameters.Add(new SqlParameter("@IsDeleted", SqlDbType.Bit) { Value = isDeleted });
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <summary>リマインダーを保存してよいか確かめる。管理者が、リマインダーを無効にした人は、保存できず、無効にした人を宛先にもできない</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">使うトランザクション</param>
    /// <param name="userId">今のユーザーの番号</param>
    /// <param name="targetUserId">宛先の人の番号。全員宛ては 0</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>確認の完了を表すタスク</returns>
    /// <exception cref="DataFileException">今のユーザー、または宛先の人が、リマインダーを無効にされている。メッセージは画面に出せる。</exception>
    private static async Task EnsureAllowedAsync(
        SqlConnection connection, SqlTransaction transaction, int userId, int targetUserId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            SELECT CASE WHEN {FeatureAccessSql.IsDisabled("@UserId", CodeFeatures.Reminders)} THEN 1
                        WHEN @TargetUserId > 0 AND {FeatureAccessSql.IsDisabled("@TargetUserId", CodeFeatures.Reminders)} THEN 2
                        ELSE 0 END
            """;
        AddUserParameter(command, userId);
        command.Parameters.Add(new SqlParameter("@TargetUserId", SqlDbType.Int) { Value = targetUserId });
        var denied = (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        if (denied != 0)
        {
            var message = denied == 1 ? "リマインダーは、管理者が無効にしているため、保存できません。" : "宛先の人は、リマインダーを使えないため、保存できません。";
            throw new DataFileException(message, new InvalidOperationException(message));
        }
    }

    /// <summary>今のユーザーの番号のパラメーターを足す</summary>
    /// <param name="command">足す先のコマンド</param>
    /// <param name="userId">今のユーザーの番号</param>
    public static void AddUserParameter(SqlCommand command, int userId)
        => command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });

    /// <summary>リマインダー本体の行のパラメーターを足す (番号・作成者・更新者は含まない)</summary>
    /// <param name="command">足す先のコマンド</param>
    /// <param name="row">リマインダー本体の行</param>
    private static void AddReminderParameters(SqlCommand command, ReminderRow row)
    {
        command.Parameters.Add(new SqlParameter("@IsDeleted", SqlDbType.Bit) { Value = row.IsDeleted });
        command.Parameters.Add(new SqlParameter("@TargetUserId", SqlDbType.Int) { Value = row.TargetUserId });
        command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = row.Date.ToDateTime(TimeOnly.MinValue) });
        command.Parameters.Add(new SqlParameter("@Time", SqlDbType.Time) { Scale = 0, Value = row.Time.ToTimeSpan() });
        command.Parameters.Add(new SqlParameter("@Weekdays", SqlDbType.TinyInt) { Value = row.Weekdays });
        command.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, 200) { Value = row.Title });
        command.Parameters.Add(new SqlParameter("@Note", SqlDbType.NVarChar, 1000) { Value = row.Note });
        command.Parameters.Add(new SqlParameter("@Link", SqlDbType.NVarChar, 2000) { Value = row.Link });
        command.Parameters.Add(new SqlParameter("@IsSpeak", SqlDbType.Bit) { Value = row.IsSpeak });
    }
}
