using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.SqlServer.Connection;
using MmmToolWeb.Auth.Core.Enrollment;

namespace MmmToolWeb.Data.SqlServer.Enrollment;

/// <summary>Web 版の認証の状態を SQL Server の dbo.AppUserWebAuth に保存する</summary>
/// <remarks>
/// 1 人に 1 行で、行が無い人はコードを出すときに作る。
/// コードの期限と更新日時は DB サーバーの時計で書く・比べる。
/// 作成者・更新者はコードを出した管理者のユーザーの番号を持てないので 0 になる。
/// この操作はユーザーのログインではなく、サーバーの PC での操作のため。
/// </remarks>
/// <param name="database">SQL Server への入口</param>
public sealed class SqlServerAppUserWebAuthRepository(SqlServerDatabase database) : IAppUserWebAuthRepository
{
    /// <summary>重複したキーのエラー番号</summary>
    private static readonly int[] DuplicateKeyErrors = [2601, 2627];

    /// <summary>行が無い人のログインの世代の番号</summary>
    /// <remarks>列の既定値と同じ</remarks>
    private const int DefaultSessionVersion = 1;

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    /// <exception cref="InvalidOperationException">別の操作と重なって、2 回続けて保存できなかった。</exception>
    public Task<DateTimeOffset> SetEnrollmentCodeAsync(int appUserId, string codeHash, TimeSpan lifetime, CancellationToken cancellationToken)
        => database.RunAsync(async connection =>
        {
            // 行があれば更新、無ければ作る。同時に 2 人が最初のコードを出して、作る側が重複したときは、もう一度更新に回る
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var expiresAt = await TryUpdateAsync(connection, appUserId, codeHash, lifetime, cancellationToken).ConfigureAwait(false)
                    ?? await TryInsertAsync(connection, appUserId, codeHash, lifetime, cancellationToken).ConfigureAwait(false);
                if (expiresAt is not null)
                {
                    return expiresAt.Value;
                }
            }
            throw new InvalidOperationException("登録用コードを保存できませんでした。もう一度実行してください。");
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<int?> FindUserByEnrollmentCodeAsync(string codeHash, CancellationToken cancellationToken)
        => database.RunAsync<int?>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT AppUserId FROM dbo.AppUserWebAuth
                WHERE EnrollmentCodeHash = @CodeHash AND EnrollmentCodeHash <> '' AND IsDeleted = 0 AND EnrollmentCodeExpiresAt > SYSDATETIMEOFFSET()
                """;
            command.Parameters.Add(new SqlParameter("@CodeHash", SqlDbType.VarChar, 64) { Value = codeHash });
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as int?;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<int?> ConsumeEnrollmentCodeAsync(string codeHash, CancellationToken cancellationToken)
        => database.RunAsync<int?>(async connection =>
        {
            // 1 つの UPDATE で「有効なコードを探す」と「使い終わりにする」を同時に行う (同じコードを同時に使っても、成功は 1 回だけ)
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.AppUserWebAuth
                SET EnrollmentCodeHash = '', EnrollmentCodeExpiresAt = '9999-12-31', UpdatedAt = SYSDATETIMEOFFSET()
                OUTPUT INSERTED.AppUserId
                WHERE EnrollmentCodeHash = @CodeHash AND EnrollmentCodeHash <> '' AND IsDeleted = 0 AND EnrollmentCodeExpiresAt > SYSDATETIMEOFFSET()
                """;
            command.Parameters.Add(new SqlParameter("@CodeHash", SqlDbType.VarChar, 64) { Value = codeHash });
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as int?;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<int> GetSessionVersionAsync(int appUserId, CancellationToken cancellationToken)
        => database.RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SessionVersion FROM dbo.AppUserWebAuth WHERE AppUserId = @AppUserId AND IsDeleted = 0";
            command.Parameters.Add(new SqlParameter("@AppUserId", SqlDbType.Int) { Value = appUserId });
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is int version ? version : DefaultSessionVersion;
        }, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    /// <exception cref="InvalidOperationException">別の操作と重なって、2 回続けて保存できなかった。</exception>
    public Task<int> IncrementSessionVersionAsync(int appUserId, CancellationToken cancellationToken)
        => database.RunAsync(async connection =>
        {
            // 行があれば番号を足す。無ければ、世代 1 の次の 2 で作る。同時に作る側が重複したときは、もう一度足す側に回る
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var update = connection.CreateCommand();
                update.CommandText =
                    """
                    UPDATE dbo.AppUserWebAuth
                    SET SessionVersion = SessionVersion + 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
                    OUTPUT INSERTED.SessionVersion
                    WHERE AppUserId = @AppUserId AND IsDeleted = 0
                    """;
                update.Parameters.Add(new SqlParameter("@AppUserId", SqlDbType.Int) { Value = appUserId });
                if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is int updated)
                {
                    return updated;
                }

                await using var insert = connection.CreateCommand();
                insert.CommandText =
                    """
                    INSERT INTO dbo.AppUserWebAuth (CreatedByUserId, UpdatedByUserId, AppUserId, SessionVersion)
                    OUTPUT INSERTED.SessionVersion
                    VALUES (0, 0, @AppUserId, @SessionVersion)
                    """;
                insert.Parameters.Add(new SqlParameter("@AppUserId", SqlDbType.Int) { Value = appUserId });
                insert.Parameters.Add(new SqlParameter("@SessionVersion", SqlDbType.Int) { Value = DefaultSessionVersion + 1 });
                try
                {
                    if (await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is int inserted)
                    {
                        return inserted;
                    }
                }
                catch (SqlException ex) when (DuplicateKeyErrors.Contains(ex.Number))
                {
                    // 同時に別の操作が行を作った。次の回で、足す側に回る
                }
            }
            throw new InvalidOperationException("ログインの世代の番号を上げられませんでした。もう一度実行してください。");
        }, cancellationToken);

    /// <summary>その人の行があればコードを入れ替える</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="appUserId">コードを出す人のユーザーの番号</param>
    /// <param name="codeHash">コードのハッシュ</param>
    /// <param name="lifetime">今から有効な時間</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 入れ替えたときの期限
    /// 行が無ければ null
    /// </returns>
    private static async Task<DateTimeOffset?> TryUpdateAsync(
        SqlConnection connection, int appUserId, string codeHash, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE dbo.AppUserWebAuth
            SET EnrollmentCodeHash = @CodeHash, EnrollmentCodeExpiresAt = DATEADD(second, @Seconds, SYSDATETIMEOFFSET()),
                UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
            OUTPUT INSERTED.EnrollmentCodeExpiresAt
            WHERE AppUserId = @AppUserId AND IsDeleted = 0
            """;
        AddCodeParameters(command, appUserId, codeHash, lifetime);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as DateTimeOffset?;
    }

    /// <summary>その人の行をコードつきで作る</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="appUserId">コードを出す人のユーザーの番号</param>
    /// <param name="codeHash">コードのハッシュ</param>
    /// <param name="lifetime">今から有効な時間</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 作ったときの期限
    /// 同時に別の操作が先に行を作ったときは null
    /// </returns>
    private static async Task<DateTimeOffset?> TryInsertAsync(
        SqlConnection connection, int appUserId, string codeHash, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO dbo.AppUserWebAuth (CreatedByUserId, UpdatedByUserId, AppUserId, EnrollmentCodeHash, EnrollmentCodeExpiresAt)
            OUTPUT INSERTED.EnrollmentCodeExpiresAt
            VALUES (0, 0, @AppUserId, @CodeHash, DATEADD(second, @Seconds, SYSDATETIMEOFFSET()))
            """;
        AddCodeParameters(command, appUserId, codeHash, lifetime);
        try
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as DateTimeOffset?;
        }
        catch (SqlException ex) when (DuplicateKeyErrors.Contains(ex.Number))
        {
            return null;
        }
    }

    /// <summary>コードを入れる SQL の値をコマンドに足す</summary>
    /// <param name="command">足し先のコマンド</param>
    /// <param name="appUserId">コードを出す人のユーザーの番号</param>
    /// <param name="codeHash">コードのハッシュ</param>
    /// <param name="lifetime">今から有効な時間</param>
    private static void AddCodeParameters(SqlCommand command, int appUserId, string codeHash, TimeSpan lifetime)
    {
        command.Parameters.Add(new SqlParameter("@AppUserId", SqlDbType.Int) { Value = appUserId });
        command.Parameters.Add(new SqlParameter("@CodeHash", SqlDbType.VarChar, 64) { Value = codeHash });
        command.Parameters.Add(new SqlParameter("@Seconds", SqlDbType.Int) { Value = (int)lifetime.TotalSeconds });
    }
}
