using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;
using MmmToolWeb.Auth.Core.Passkeys;

namespace MmmToolWeb.Data.SqlServer.Passkeys;

/// <summary>
/// パスキー (<see cref="AppUserPasskey"/>)を、SQL Server の <c>dbo.AppUserPasskey</c> に保存する。
/// </summary>
/// <remarks>
/// 作成者・更新者は、パスキーの持ち主 (本人が登録し、本人がログインで更新するため)。更新日時は DB サーバーの時計で書く。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerAppUserPasskeyRepository(SqlServerDatabase database) : IAppUserPasskeyRepository
{
    /// <summary>読む列 (<see cref="ReadPasskey"/> の並び)</summary>
    private const string Columns = "Id, AppUserId, CredentialId, PublicKey, SignCount, UserHandle, DeviceName, LastUsedAt";

    /// <summary>端末の呼び名の最大文字数 (DB の列の長さと同じ)</summary>
    private const int DeviceNameMaxLength = 50;

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<AppUserPasskey?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken)
        => database.RunAsync<AppUserPasskey?>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM dbo.AppUserPasskey WHERE CredentialId = @CredentialId AND IsDeleted = 0";
            command.Parameters.Add(new SqlParameter("@CredentialId", SqlDbType.VarBinary, 1024) { Value = credentialId });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadPasskey(reader) : null;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・読めなかった (メッセージは画面に出せる)。</exception>
    public Task<IReadOnlyList<AppUserPasskey>> GetByUserAsync(int appUserId, CancellationToken cancellationToken)
        => database.RunAsync<IReadOnlyList<AppUserPasskey>>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM dbo.AppUserPasskey WHERE AppUserId = @AppUserId AND IsDeleted = 0 ORDER BY Id";
            command.Parameters.Add(new SqlParameter("@AppUserId", SqlDbType.Int) { Value = appUserId });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var passkeys = new List<AppUserPasskey>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                passkeys.Add(ReadPasskey(reader));
            }
            return passkeys;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task AddAsync(AppUserPasskey passkey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passkey);

        return database.RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO dbo.AppUserPasskey
                    (CreatedByUserId, UpdatedByUserId, AppUserId, CredentialId, PublicKey, SignCount, UserHandle, DeviceName, LastUsedAt)
                VALUES (@AppUserId, @AppUserId, @AppUserId, @CredentialId, @PublicKey, @SignCount, @UserHandle, @DeviceName, SYSDATETIMEOFFSET())
                """;
            command.Parameters.Add(new SqlParameter("@AppUserId", SqlDbType.Int) { Value = passkey.AppUserId });
            command.Parameters.Add(new SqlParameter("@CredentialId", SqlDbType.VarBinary, 1024) { Value = passkey.CredentialId });
            command.Parameters.Add(new SqlParameter("@PublicKey", SqlDbType.VarBinary, 1024) { Value = passkey.PublicKey });
            command.Parameters.Add(new SqlParameter("@SignCount", SqlDbType.BigInt) { Value = (long)passkey.SignCount });
            command.Parameters.Add(new SqlParameter("@UserHandle", SqlDbType.VarBinary, 64) { Value = passkey.UserHandle });
            command.Parameters.Add(new SqlParameter("@DeviceName", SqlDbType.NVarChar, DeviceNameMaxLength)
            {
                Value = passkey.DeviceName.Length > DeviceNameMaxLength ? passkey.DeviceName[..DeviceNameMaxLength] : passkey.DeviceName,
            });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">設定が足りない・接続できない・保存できなかった (メッセージは画面に出せる)。</exception>
    public Task UpdateUsageAsync(int id, uint signCount, DateTime lastUsedAt, CancellationToken cancellationToken)
        => database.RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.AppUserPasskey
                SET SignCount = @SignCount, LastUsedAt = SYSDATETIMEOFFSET(), UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = AppUserId
                WHERE Id = @Id AND IsDeleted = 0
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@SignCount", SqlDbType.BigInt) { Value = (long)signCount });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>読んでいる行を、<see cref="Columns"/> の並びの <see cref="AppUserPasskey"/> にする</summary>
    /// <param name="reader">読んでいる位置のリーダー</param>
    /// <returns>パスキー</returns>
    private static AppUserPasskey ReadPasskey(SqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetInt32(1),
        reader.GetFieldValue<byte[]>(2),
        reader.GetFieldValue<byte[]>(3),
        (uint)reader.GetInt64(4),
        reader.GetFieldValue<byte[]>(5),
        reader.GetString(6),
        reader.GetDateTimeOffset(7).UtcDateTime);
}
