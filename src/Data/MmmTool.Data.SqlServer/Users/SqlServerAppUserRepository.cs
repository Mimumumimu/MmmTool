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

    /// <summary>ハッシュ以外の列を選ぶ SELECT の列 (ハッシュは <see cref="FindByLoginNameAsync"/> だけが読む)</summary>
    private const string Columns = "Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, DisplayName, LoginName, ValidFrom, ValidTo";

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken = default)
        => database.RunAsync<IReadOnlyList<AppUser>>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM dbo.AppUser ORDER BY Id";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var users = new List<AppUser>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                users.Add(ReadRow(reader).ToAppUser());
            }
            return users;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<AppUserCredential?> FindByLoginNameAsync(string loginName, CancellationToken cancellationToken = default)
        => database.RunAsync<AppUserCredential?>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns}, PasswordHash FROM dbo.AppUser WHERE LoginName = @LoginName AND IsDeleted = 0";
            command.Parameters.Add(new SqlParameter("@LoginName", SqlDbType.NVarChar, AppUser.LoginNameMaxLength) { Value = loginName });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
            return new AppUserCredential(ReadRow(reader).ToAppUser(), reader.GetString(10));
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">表示名・ログイン名・使える期間が正しくない。</exception>
    /// <exception cref="DataFileException">設定が足りない・接続できない・保存できなかった (メッセージは画面に出せる)。</exception>
    /// <exception cref="LoginNameTakenException">同じログイン名がすでにある。</exception>
    public Task<AppUser> AddAsync(AppUser user, string passwordHash, CancellationToken cancellationToken = default)
    {
        var row = AppUserRow.FromAppUser(user with { Id = 0, IsDeleted = false }, passwordHash);
        return database.RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO dbo.AppUser (CreatedByUserId, UpdatedByUserId, DisplayName, LoginName, PasswordHash, ValidFrom, ValidTo)
                OUTPUT INSERTED.Id
                VALUES (0, 0, @DisplayName, @LoginName, @PasswordHash, @ValidFrom, @ValidTo)
                """;
            command.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, AppUser.DisplayNameMaxLength) { Value = row.DisplayName });
            command.Parameters.Add(new SqlParameter("@LoginName", SqlDbType.NVarChar, AppUser.LoginNameMaxLength) { Value = row.LoginName });
            command.Parameters.Add(new SqlParameter("@PasswordHash", SqlDbType.VarChar, 200) { Value = row.PasswordHash });
            command.Parameters.Add(new SqlParameter("@ValidFrom", SqlDbType.Date) { Value = row.ValidFrom.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@ValidTo", SqlDbType.Date) { Value = row.ValidTo.ToDateTime(TimeOnly.MinValue) });

            try
            {
                var id = (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
                return (row with { Id = id }).ToAppUser();
            }
            catch (SqlException ex) when (DuplicateKeyErrors.Contains(ex.Number))
            {
                throw new LoginNameTakenException(ex);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task<bool> SetPasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
        => database.RunAsync(async connection =>
        {
            // 空のときだけ入れる (ほかの人が先に決めたパスワードを、上書きしないため)
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.AppUser
                SET PasswordHash = @PasswordHash, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @UserId AND IsDeleted = 0 AND PasswordHash = ''
                """;
            command.Parameters.Add(new SqlParameter("@PasswordHash", SqlDbType.VarChar, 200) { Value = passwordHash });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">表示名・ログイン名が正しくない。</exception>
    /// <exception cref="DataFileException">設定が足りない・接続できない・保存できなかった (メッセージは画面に出せる)。</exception>
    /// <exception cref="LoginNameTakenException">同じログイン名がすでにある。</exception>
    public Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        var row = AppUserRow.FromAppUser(user, "");
        return database.RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.AppUser
                SET DisplayName = @DisplayName, LoginName = @LoginName, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @UserId AND IsDeleted = 0
                """;
            command.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, AppUser.DisplayNameMaxLength) { Value = row.DisplayName });
            command.Parameters.Add(new SqlParameter("@LoginName", SqlDbType.NVarChar, AppUser.LoginNameMaxLength) { Value = row.LoginName });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = row.Id });

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex) when (DuplicateKeyErrors.Contains(ex.Number))
            {
                throw new LoginNameTakenException(ex);
            }
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task<bool> ChangePasswordHashAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
        => database.RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.AppUser
                SET PasswordHash = @PasswordHash, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @UserId AND IsDeleted = 0
                """;
            command.Parameters.Add(new SqlParameter("@PasswordHash", SqlDbType.VarChar, 200) { Value = passwordHash });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }, cancellationToken);

    /// <summary>読んでいる行を、<see cref="Columns"/> の並びの <see cref="AppUserRow"/> にする</summary>
    /// <param name="reader">読んでいる位置のリーダー</param>
    /// <returns>DB の行 (パスワードのハッシュは含まない)</returns>
    private static AppUserRow ReadRow(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        IsDeleted = reader.GetBoolean(1),
        CreatedAt = reader.GetDateTimeOffset(2),
        CreatedByUserId = reader.GetInt32(3),
        UpdatedAt = reader.GetDateTimeOffset(4),
        UpdatedByUserId = reader.GetInt32(5),
        DisplayName = reader.GetString(6),
        LoginName = reader.GetString(7),
        ValidFrom = DateOnly.FromDateTime(reader.GetDateTime(8)),
        ValidTo = DateOnly.FromDateTime(reader.GetDateTime(9)),
    };
}
