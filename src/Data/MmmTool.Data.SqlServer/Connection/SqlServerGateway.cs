using MmmSdk.Core.Components.Storage;
using MmmTool.Data.Queries;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Connection;

/// <summary>SQL Server で処理を実行する入口</summary>
/// <param name="database">SQL Server に接続して処理を実行する入口</param>
/// <param name="currentUser">今のユーザー (記録の持ち主・作成者・更新者)</param>
/// <param name="featureName">
/// 機能の名前
/// ユーザーを特定していないときのメッセージに入れる
/// </param>
/// <remarks>
/// 更新日時は更新の SQL の中で DB サーバーの時計を使って書くので、アプリは日時を渡さない。
/// ユーザーを特定していないときの書き込みは、何も書かない。
/// </remarks>
internal sealed class SqlServerGateway(SqlServerDatabase database, CurrentUser currentUser, string featureName) : DbGateway
{
    /// <inheritdoc />
    public override SqlDialect Dialect => SqlServerDialect.Instance;

    /// <inheritdoc />
    /// <remarks>
    /// ユーザーを特定していないときは 0
    /// 読み込みは何も返さない
    /// </remarks>
    public override int UserId => currentUser.User?.Id ?? 0;

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない
    /// ログインが必要
    /// </exception>
    public override int RequireUserId()
        => currentUser.User?.Id
            ?? throw new DataFileException($"ユーザーが特定されていないため、{featureName}を使えません。ログインが必要です。", new InvalidOperationException("今のユーザーが特定されていません。"));

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// 設定が足りない・接続できない・実行中に失敗した
    /// メッセージは画面に出せる
    /// </exception>
    public override Task<T> ReadAsync<T>(Func<DbSession, Task<T>> work, CancellationToken cancellationToken)
        => database.RunAsync(connection => work(new DbSession(connection, null, Dialect, UserId, DateTimeOffset.UtcNow)), cancellationToken);

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// ユーザーを特定していない・接続できない・保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    public override Task WriteAsync(Func<DbSession, Task> work, CancellationToken cancellationToken)
        => database.RunAsync(
            async connection =>
            {
                var user = RequireUserId();
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await work(new DbSession(connection, transaction, Dialect, user, DateTimeOffset.UtcNow)).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
}
