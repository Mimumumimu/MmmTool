using Microsoft.Data.Sqlite;

namespace MmmTool.Data.Sqlite;

/// <summary>SQLite の問い合わせを実行する共通の補助 (1 件ずつ読む・1 つの値を読む・値を返さない SQL を実行する)</summary>
/// <remarks>パラメーターは、名前 (<c>@</c> つき)と値の組。値は <see cref="SqliteValues.Command"/> が、保存する形に変える。</remarks>
internal static class SqliteQueries
{
    /// <summary>検索の結果を、すべて読む</summary>
    /// <typeparam name="T">1 行を変換した型</typeparam>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="map">1 行を変換する処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変換した行の一覧</returns>
    public static async Task<IReadOnlyList<T>> QueryAsync<T>(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        (string, object)[] parameters,
        Func<SqliteDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var command = SqliteValues.Command(connection, transaction, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(map(reader));
        }
        return list;
    }

    /// <summary>1 つの値を返す SQL を実行する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>1 行 1 列目の値。行が無ければ null</returns>
    public static async Task<object?> ScalarAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = SqliteValues.Command(connection, transaction, sql, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    /// <summary>値を返さない SQL を実行する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL</param>
    /// <param name="parameters">パラメーター</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変わった行の数</returns>
    public static async Task<int> ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, (string, object)[] parameters, CancellationToken cancellationToken)
    {
        await using var command = SqliteValues.Command(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
