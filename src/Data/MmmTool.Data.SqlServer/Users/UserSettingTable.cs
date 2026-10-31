using System.Data;
using Microsoft.Data.SqlClient;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Reminders;

namespace MmmTool.Data.SqlServer.Users;

/// <summary>
/// 縦持ちの設定の表 (<c>dbo.AppUserSetting</c>・<c>dbo.AppUserAdminSetting</c>)の読み書き。2 つの表は列とキーが同じなので、表の名前だけを変えて使う。
/// </summary>
/// <remarks>
/// 設定を外すときは論理削除する。外した行は、設定し直すときに、削除を戻して値を直す。
/// 同じ人・同じ設定の、削除されていない行は 1 行だけで、一意の索引が守る。
/// 作成者と更新者の列には、操作した人の番号を入れる。管理者の設定は、管理ツールが書くので 0。
/// </remarks>
internal sealed class UserSettingTable
{
    /// <summary>値の最大の長さ <c>Value</c></summary>
    public const int ValueMaxLength = 200;

    /// <summary>ユーザー自身が決める設定の表</summary>
    public static readonly UserSettingTable AppUserSetting = new("AppUserSetting", CodeFeatures.Common);

    /// <summary>管理者がユーザーごとに決める設定の表</summary>
    public static readonly UserSettingTable AppUserAdminSetting = new("AppUserAdminSetting", CodeFeatures.Common + 1);

    /// <summary>表の名前</summary>
    private readonly string _table;

    /// <summary>この表で使える一番小さい機能番号。共通の <see cref="CodeFeatures.Common"/> を使うのは、ユーザー自身が決める設定だけ</summary>
    private readonly int _minFeatureNo;

    /// <summary>表の名前から作る</summary>
    /// <param name="table">表の名前。定数だけを渡す (SQL に埋め込むため)</param>
    /// <param name="minFeatureNo">この表で使える一番小さい機能番号</param>
    private UserSettingTable(string table, int minFeatureNo)
    {
        _table = table;
        _minFeatureNo = minFeatureNo;
    }

    /// <summary>設定の値を読む</summary>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="userId">設定を持つ人の番号</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>値。設定が無いときは null</returns>
    public Task<string?> GetAsync(SqlServerDatabase database, int userId, int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT Value FROM dbo.{_table}
                WHERE UserId = @UserId AND FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 0
                """;
            AddKey(command, userId, featureNo, categoryNo, codeNo);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }, cancellationToken);

    /// <summary>1 人の、同じ区分・コード番号の設定を、機能ごとに読む</summary>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="userId">設定を持つ人の番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>機能番号と値の組</returns>
    public Task<IReadOnlyDictionary<int, string>> GetByFeatureAsync(SqlServerDatabase database, int userId, int categoryNo, int codeNo, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync<IReadOnlyDictionary<int, string>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT FeatureNo, Value FROM dbo.{_table}
                WHERE UserId = @UserId AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 0
                """;
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@CategoryNo", SqlDbType.Int) { Value = categoryNo });
            command.Parameters.Add(new SqlParameter("@CodeNo", SqlDbType.Int) { Value = codeNo });
            var values = new Dictionary<int, string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                values[reader.GetInt32(0)] = reader.GetString(1);
            }

            return values;
        }, cancellationToken);

    /// <summary>同じ設定の値を持つ人の番号を読む</summary>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="value">探す値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>人の番号</returns>
    public Task<IReadOnlyList<int>> GetUserIdsAsync(SqlServerDatabase database, int featureNo, int categoryNo, int codeNo, string value, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync<IReadOnlyList<int>>(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT UserId FROM dbo.{_table}
                WHERE FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND Value = @Value AND IsDeleted = 0
                """;
            command.Parameters.Add(new SqlParameter("@FeatureNo", SqlDbType.Int) { Value = featureNo });
            command.Parameters.Add(new SqlParameter("@CategoryNo", SqlDbType.Int) { Value = categoryNo });
            command.Parameters.Add(new SqlParameter("@CodeNo", SqlDbType.Int) { Value = codeNo });
            command.Parameters.Add(new SqlParameter("@Value", SqlDbType.NVarChar, ValueMaxLength) { Value = value });
            var userIds = new List<int>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                userIds.Add(reader.GetInt32(0));
            }

            return userIds;
        }, cancellationToken);

    /// <summary>設定の値を保存する</summary>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="userId">設定を持つ人の番号</param>
    /// <param name="actorUserId">操作した人の番号。作成者・更新者の列に入れる</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="value">値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="ArgumentException">番号が小さすぎる・値が長すぎる。</exception>
    public Task SetAsync(SqlServerDatabase database, int userId, int actorUserId, int featureNo, int categoryNo, int codeNo, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (featureNo < _minFeatureNo || categoryNo < 1 || codeNo < 1)
        {
            throw new ArgumentException($"設定の番号が正しくありません: {featureNo}・{categoryNo}・{codeNo}。");
        }
        if (value.Length > ValueMaxLength)
        {
            throw new ArgumentException($"設定の値は {ValueMaxLength} 文字までにしてください。", nameof(value));
        }

        return SendTableAccess.RunAsync(database, async connection =>
        {
            // 削除されていない行があれば値を直す。無ければ、外した行の削除を戻す。それも無ければ足す
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SET XACT_ABORT ON;
                BEGIN TRAN;
                UPDATE dbo.{_table} SET Value = @Value, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @ActorUserId
                WHERE UserId = @UserId AND FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 0;
                IF @@ROWCOUNT = 0
                BEGIN
                    UPDATE TOP (1) dbo.{_table} SET Value = @Value, IsDeleted = 0, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @ActorUserId
                    WHERE UserId = @UserId AND FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 1;
                    IF @@ROWCOUNT = 0
                        INSERT INTO dbo.{_table} (CreatedByUserId, UpdatedByUserId, UserId, FeatureNo, CategoryNo, CodeNo, Value)
                        VALUES (@ActorUserId, @ActorUserId, @UserId, @FeatureNo, @CategoryNo, @CodeNo, @Value);
                END
                COMMIT TRAN;
                """;
            AddKey(command, userId, featureNo, categoryNo, codeNo);
            command.Parameters.Add(new SqlParameter("@ActorUserId", SqlDbType.Int) { Value = actorUserId });
            command.Parameters.Add(new SqlParameter("@Value", SqlDbType.NVarChar, ValueMaxLength) { Value = value });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    /// <summary>設定を外す (論理削除する)</summary>
    /// <param name="database">SQL Server への入口</param>
    /// <param name="userId">設定を持つ人の番号</param>
    /// <param name="actorUserId">操作した人の番号。更新者の列に入れる</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>外す処理の完了を表すタスク。設定が無くても、失敗にしない</returns>
    public Task ClearAsync(SqlServerDatabase database, int userId, int actorUserId, int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken)
        => SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                UPDATE dbo.{_table} SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @ActorUserId
                WHERE UserId = @UserId AND FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 0
                """;
            AddKey(command, userId, featureNo, categoryNo, codeNo);
            command.Parameters.Add(new SqlParameter("@ActorUserId", SqlDbType.Int) { Value = actorUserId });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

    /// <summary>設定を決める 4 つの番号を、パラメーターに足す</summary>
    /// <param name="command">足す先のコマンド</param>
    /// <param name="userId">人の番号</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号</param>
    private static void AddKey(SqlCommand command, int userId, int featureNo, int categoryNo, int codeNo)
    {
        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
        command.Parameters.Add(new SqlParameter("@FeatureNo", SqlDbType.Int) { Value = featureNo });
        command.Parameters.Add(new SqlParameter("@CategoryNo", SqlDbType.Int) { Value = categoryNo });
        command.Parameters.Add(new SqlParameter("@CodeNo", SqlDbType.Int) { Value = codeNo });
    }
}
