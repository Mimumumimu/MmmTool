using System.Data;
using Microsoft.Data.SqlClient;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.SqlServer.Reminders;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Users;

/// <summary>
/// ユーザーごとの設定 (<c>dbo.AppUserSetting</c>)の保存を、SQL Server で行う。
/// </summary>
/// <remarks>
/// 読むたびに DB から読む。操作するのは今のユーザー自身の設定だけで、特定していないと使えない。
/// 設定を外すときは論理削除する。外した行は、設定し直すときに、削除を戻して値を直す。
/// 同じ人・同じ設定の、削除されていない行は 1 行だけで、一意の索引が守る。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
public sealed class SqlServerAppUserSettingRepository(SqlServerDatabase database, CurrentUser currentUser) : IAppUserSettingRepository
{
    /// <summary>値の最大の長さ <c>AppUserSetting.Value</c></summary>
    private const int ValueMaxLength = 200;

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public Task<string?> GetAsync(int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Value FROM dbo.AppUserSetting
                WHERE UserId = @UserId AND FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 0
                """;
            AddKey(command, userId, featureNo, categoryNo, codeNo);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">番号が 1 未満・値が長すぎる。</exception>
    public Task SetAsync(int featureNo, int categoryNo, int codeNo, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (featureNo < 1 || categoryNo < 1 || codeNo < 1)
        {
            throw new ArgumentException($"設定の番号が正しくありません: {featureNo}・{categoryNo}・{codeNo}。");
        }
        if (value.Length > ValueMaxLength)
        {
            throw new ArgumentException($"設定の値は {ValueMaxLength} 文字までにしてください。", nameof(value));
        }

        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            // 削除されていない行があれば値を直す。無ければ、外した行の削除を戻す。それも無ければ足す
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SET XACT_ABORT ON;
                BEGIN TRAN;
                UPDATE dbo.AppUserSetting SET Value = @Value, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE UserId = @UserId AND FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 0;
                IF @@ROWCOUNT = 0
                BEGIN
                    UPDATE TOP (1) dbo.AppUserSetting SET Value = @Value, IsDeleted = 0, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                    WHERE UserId = @UserId AND FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 1;
                    IF @@ROWCOUNT = 0
                        INSERT INTO dbo.AppUserSetting (CreatedByUserId, UpdatedByUserId, UserId, FeatureNo, CategoryNo, CodeNo, Value)
                        VALUES (@UserId, @UserId, @UserId, @FeatureNo, @CategoryNo, @CodeNo, @Value);
                END
                COMMIT TRAN;
                """;
            AddKey(command, userId, featureNo, categoryNo, codeNo);
            command.Parameters.Add(new SqlParameter("@Value", SqlDbType.NVarChar, ValueMaxLength) { Value = value });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearAsync(int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default)
    {
        var userId = SendTableAccess.RequireUserId(currentUser);
        return SendTableAccess.RunAsync(database, async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.AppUserSetting SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE UserId = @UserId AND FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 0
                """;
            AddKey(command, userId, featureNo, categoryNo, codeNo);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

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
