using System.Data.Common;

namespace MmmTool.Data.Queries;

/// <summary>開いた接続での問い合わせ</summary>
/// <remarks>
/// 接続・トランザクション・今のユーザー・<see cref="SqlDialect"/> を 1 つにまとめ、SQL の実行を受け持つ。
/// 書き込みの接続 (トランザクションあり)では、全部の SQL にパラメーター <c>@user</c> を付ける。
/// 更新日時をアプリが渡す種類では、<c>@now</c> も付ける。
/// </remarks>
/// <param name="connection">開いた接続</param>
/// <param name="transaction">トランザクション (読み込みでは null)</param>
/// <param name="dialect">DB の種類ごとの SQL の書き方</param>
/// <param name="userId">今のユーザーの番号</param>
/// <param name="now">更新日時としてアプリが渡す日時</param>
public sealed class DbSession(DbConnection connection, DbTransaction? transaction, SqlDialect dialect, int userId, DateTimeOffset now)
{
    /// <summary>DB の種類ごとの SQL の書き方</summary>
    public SqlDialect Dialect => dialect;

    /// <summary>今のユーザーの番号</summary>
    /// <remarks>特定していない読み込みでは 0</remarks>
    public int UserId => userId;

    /// <summary>検索の結果をすべて読む</summary>
    /// <typeparam name="T">1 行を変換した型</typeparam>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="map">1 行を変換する処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変換した行の一覧</returns>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, (string, object)[] parameters, Func<DbDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = Command(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(map(reader));
        }
        return list;
    }

    /// <summary>複数の SELECT のまとめ読み</summary>
    /// <param name="queries">
    /// <c>SELECT</c> と、その 1 行を受け取る処理の並び。
    /// 結果はこの順に届く。
    /// </param>
    /// <param name="parameters">パラメーター (すべての <c>SELECT</c> で共有する)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>
    /// 1 つのコマンドで送り、1 回の往復で結果を順に受け取る。
    /// </remarks>
    public async Task QueryBatchAsync((string Sql, Action<DbDataReader> Map)[] queries, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = Command(string.Join(";\n", queries.Select(q => q.Sql)), parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        for (var i = 0; i < queries.Length; i++)
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                queries[i].Map(reader);
            }
            if (i < queries.Length - 1 && !await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("まとめて送った問い合わせの結果が、足りません。");
            }
        }
    }

    /// <summary>1 つの値を返す SQL を実行する</summary>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 1 行 1 列目の値
    /// 行が無ければ null
    /// </returns>
    public async Task<object?> ScalarAsync(string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = Command(sql, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    /// <summary>値を返さない SQL を実行する</summary>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変わった行の数</returns>
    public async Task<int> ExecuteAsync(string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = Command(sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>パラメーターつきのコマンド</summary>
    /// <param name="sql">SQL (値はパラメーターで渡す)</param>
    /// <param name="parameters">パラメーターの名前 (<c>@</c> つき)と値</param>
    /// <returns>
    /// コマンド
    /// 呼ぶ側が破棄する
    /// </returns>
    private DbCommand Command(string sql, (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        if (transaction is not null)
        {
            dialect.AddParameter(command, "@user", userId);
            if (dialect.UsesClientClock)
            {
                dialect.AddParameter(command, "@now", now);
            }
        }
        foreach (var (name, value) in parameters)
        {
            dialect.AddParameter(command, name, value);
        }
        return command;
    }
}
