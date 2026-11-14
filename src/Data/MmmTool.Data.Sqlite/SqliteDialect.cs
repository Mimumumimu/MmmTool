using System.Data.Common;
using System.Globalization;
using MmmTool.Data.Queries;

namespace MmmTool.Data.Sqlite;

/// <summary>SQLite の SQL の書き方</summary>
/// <remarks>
/// 表にスキーマは無い。
/// サーバーの時計が無いので、更新日時はアプリが <c>@now</c> で渡す。
/// 書き込みを 1 つずつ待ち合わせるので、行のロックの指定は要らない。
/// 番号の一覧は <c>json_each</c> で行に戻す。
/// 日時・日付は文字、真偽・小さな整数は整数、時間は実数で保存する (<see cref="SqliteValues"/>)。
/// </remarks>
internal sealed class SqliteDialect : SqlDialect
{
    /// <summary>唯一のインスタンス</summary>
    public static SqliteDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Now => "@now";

    /// <inheritdoc />
    public override bool UsesClientClock => true;

    /// <inheritdoc />
    public override string RowLockHint => "";

    /// <inheritdoc />
    public override string Table(string name) => name;

    /// <inheritdoc />
    public override void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = SqliteValues.ToStoredValue(value);
        command.Parameters.Add(parameter);
    }

    /// <inheritdoc />
    public override string IdSelect(string parameter) => $"SELECT value FROM json_each({parameter})";

    /// <inheritdoc />
    public override string IsNotBlank(string column) => $"TRIM({column}) <> ''";

    /// <inheritdoc />
    public override string InsertReturning(string table, string columns, string values, string returning)
        => $"INSERT INTO {table} (IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, {columns}) VALUES (0, @now, @user, @now, @user, {values}) RETURNING {returning}";

    /// <inheritdoc />
    public override string UpdateReturning(string table, string set, string where, string returning, bool serialize = false)
        => $"UPDATE {table} SET {set} WHERE {where} RETURNING {returning}";

    /// <inheritdoc />
    public override bool ReadBool(DbDataReader reader, int index) => reader.GetInt64(index) != 0;

    /// <inheritdoc />
    public override DateTimeOffset ReadDateTime(DbDataReader reader, int index) => SqliteValues.ParseDateTime(reader.GetString(index));

    /// <inheritdoc />
    public override DateOnly ReadDate(DbDataReader reader, int index) => SqliteValues.ParseDate(reader.GetString(index));

    /// <inheritdoc />
    public override decimal ReadHours(DbDataReader reader, int index) => SqliteValues.ToHours(reader.GetDouble(index));

    /// <inheritdoc />
    public override byte ReadByte(DbDataReader reader, int index) => checked((byte)reader.GetInt64(index));

    /// <inheritdoc />
    public override DateOnly ToDate(object value) => SqliteValues.ParseDate(Convert.ToString(value, CultureInfo.InvariantCulture)!);
}
