using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.Data.Queries;

namespace MmmTool.Data.Sqlite;

/// <summary>ローカルの SQLite で処理を実行する入口</summary>
/// <param name="database">データベースファイルを開く入口</param>
/// <param name="time">更新日時を書くための時計 (SQLite にはサーバーの時計が無いので、アプリが書く)。</param>
/// <remarks>
/// ユーザーは <c>Id = 1</c> の固定値で、<c>AppUser</c> の表は持たない。
/// 書き込みは書き込みの許可を最初に取るトランザクション (<c>BEGIN IMMEDIATE</c>)の中で行う。
/// 処理は UI スレッドを止めないよう、別のスレッドで行う。
/// </remarks>
internal sealed class SqliteGateway(SqliteDatabase database, TimeProvider time) : DbGateway
{
    /// <summary>ローカルのユーザーの番号</summary>
    private const int LocalUserId = 1;

    /// <inheritdoc />
    public override SqlDialect Dialect => SqliteDialect.Instance;

    /// <inheritdoc />
    public override int UserId => LocalUserId;

    /// <inheritdoc />
    public override int RequireUserId() => LocalUserId;

    /// <inheritdoc />
    public override Task<T> ReadAsync<T>(Func<DbSession, Task<T>> work, CancellationToken cancellationToken)
        => Task.Run(
            () => database.RunAsync(connection => work(new DbSession(connection, null, Dialect, LocalUserId, time.GetUtcNow())), cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public override Task WriteAsync(Func<DbSession, Task> work, CancellationToken cancellationToken)
        => Task.Run(
            () => database.RunInTransactionAsync((connection, transaction) => work(new DbSession(connection, transaction, Dialect, LocalUserId, time.GetUtcNow())), cancellationToken),
            cancellationToken);
}
