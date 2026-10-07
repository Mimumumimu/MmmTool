using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Data.Users;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Users;

/// <summary>
/// ユーザー (<see cref="AppUser"/>)を、SQL Server の <c>dbo.AppUser</c> に保存する。
/// </summary>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerAppUserRepository(SqlServerDatabase database) : IAppUserRepository
{
    /// <summary>重複したキー (一意制約・一意インデックス)のエラー番号</summary>
    private static readonly int[] DuplicateKeyErrors = [2601, 2627];

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken = default)
        => database.RunAsync<IReadOnlyList<AppUser>>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, DisplayName, MacAddress, ValidFrom, ValidTo
                FROM dbo.AppUser
                ORDER BY Id
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var users = new List<AppUser>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                users.Add(new AppUserRow
                {
                    Id = reader.GetInt32(0),
                    IsDeleted = reader.GetBoolean(1),
                    CreatedAt = reader.GetDateTimeOffset(2),
                    CreatedByUserId = reader.GetInt32(3),
                    UpdatedAt = reader.GetDateTimeOffset(4),
                    UpdatedByUserId = reader.GetInt32(5),
                    DisplayName = reader.GetString(6),
                    MacAddress = reader.GetString(7),
                    ValidFrom = DateOnly.FromDateTime(reader.GetDateTime(8)),
                    ValidTo = DateOnly.FromDateTime(reader.GetDateTime(9)),
                }.ToAppUser());
            }
            return users;
        }, cancellationToken);

    /// <inheritdoc />
    /// <remarks>同じ MAC アドレスの、削除されていない行 (使えない期間の行)は、先に削除済みにしてから登録する (同じトランザクション)。</remarks>
    /// <exception cref="ArgumentException">表示名・MAC アドレス・使える期間が正しくない。</exception>
    /// <exception cref="DataFileException">設定が足りない・接続できない・保存できなかった・同じ MAC アドレスが同時に登録された (メッセージは画面に出せる)。</exception>
    public Task<AppUser> AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        var row = AppUserRow.FromAppUser(user with { Id = 0, IsDeleted = false });
        return database.RunAsync(async connection =>
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // 登録のときは、この PC の MAC に当てはまる、使える行が無い (特定できなかった)ので、残っているのは、削除済みか、期間の外の行だけ
            await using var retire = connection.CreateCommand();
            retire.Transaction = transaction;
            retire.CommandText =
                """
                UPDATE dbo.AppUser
                SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
                WHERE MacAddress = @MacAddress AND IsDeleted = 0
                """;
            retire.Parameters.Add(new SqlParameter("@MacAddress", SqlDbType.Char, 12) { Value = row.MacAddress });

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO dbo.AppUser (CreatedByUserId, UpdatedByUserId, DisplayName, MacAddress, ValidFrom, ValidTo)
                OUTPUT INSERTED.Id
                VALUES (0, 0, @DisplayName, @MacAddress, @ValidFrom, @ValidTo)
                """;
            command.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, 50) { Value = row.DisplayName });
            command.Parameters.Add(new SqlParameter("@MacAddress", SqlDbType.Char, 12) { Value = row.MacAddress });
            command.Parameters.Add(new SqlParameter("@ValidFrom", SqlDbType.Date) { Value = row.ValidFrom.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@ValidTo", SqlDbType.Date) { Value = row.ValidTo.ToDateTime(TimeOnly.MinValue) });

            try
            {
                await retire.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                var id = (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return (row with { Id = id }).ToAppUser();
            }
            catch (SqlException ex) when (DuplicateKeyErrors.Contains(ex.Number))
            {
                throw new DataFileException("この PC の MAC アドレスは、すでに別のユーザーとして登録されています。", ex);
            }
        }, cancellationToken);
    }
}
