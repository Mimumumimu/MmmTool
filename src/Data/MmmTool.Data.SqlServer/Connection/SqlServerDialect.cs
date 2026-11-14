using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using MmmTool.Data.Queries;

namespace MmmTool.Data.SqlServer.Connection;

/// <summary>SQL Server の SQL の書き方</summary>
/// <remarks>
/// 表は <c>dbo</c> スキーマ
/// 更新日時は更新の SQL の中で DB サーバーの時計 (<c>SYSDATETIMEOFFSET()</c>)を使って書く
/// 番号の一覧は <c>OPENJSON</c> で行に戻す
/// 書いた行は <c>OUTPUT</c> で同じ往復の中で受け取る
/// </remarks>
internal sealed class SqlServerDialect : SqlDialect
{
    /// <summary>文字列のパラメーターを固定の長さで作る上限</summary>
    /// <remarks>
    /// これより長い文字は、長さの上限が無い型 (<c>nvarchar(max)</c>)にする。
    /// 切り詰めないため。
    /// </remarks>
    private const int FixedTextLength = 4000;

    /// <summary>唯一のインスタンス</summary>
    public static SqlServerDialect Instance { get; } = new();

    /// <inheritdoc />
    public override string Now => "SYSDATETIMEOFFSET()";

    /// <inheritdoc />
    public override bool UsesClientClock => false;

    /// <inheritdoc />
    public override string RowLockHint => " WITH (UPDLOCK, ROWLOCK)";

    /// <inheritdoc />
    public override string Table(string name) => $"dbo.{name}";

    /// <inheritdoc />
    public override void AddParameter(DbCommand command, string name, object value)
        => command.Parameters.Add(value switch
        {
            int number => new SqlParameter(name, SqlDbType.Int) { Value = number },
            long number => new SqlParameter(name, SqlDbType.BigInt) { Value = number },
            byte number => new SqlParameter(name, SqlDbType.TinyInt) { Value = number },
            bool flag => new SqlParameter(name, SqlDbType.Bit) { Value = flag },
            string text => new SqlParameter(name, SqlDbType.NVarChar, text.Length <= FixedTextLength ? FixedTextLength : -1) { Value = text },
            decimal number => new SqlParameter(name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = number },
            DateOnly date => new SqlParameter(name, SqlDbType.Date) { Value = date.ToDateTime(TimeOnly.MinValue) },
            DateTime date => new SqlParameter(name, SqlDbType.Date) { Value = date },
            DateTimeOffset dateTime => new SqlParameter(name, SqlDbType.DateTimeOffset) { Scale = 3, Value = dateTime },
            _ => throw new ArgumentException($"パラメーター {name} の型 {value.GetType().Name} を扱えません。", nameof(value)),
        });

    /// <inheritdoc />
    public override string IdSelect(string parameter) => $"SELECT Id FROM OPENJSON({parameter}) WITH (Id int '$')";

    /// <inheritdoc />
    public override string IsNotBlank(string column) => $"LTRIM(RTRIM({column})) <> N''";

    /// <inheritdoc />
    public override string InsertReturning(string table, string columns, string values, string returning)
        => $"INSERT INTO {Table(table)} (IsDeleted, CreatedByUserId, UpdatedByUserId, {columns}) OUTPUT {Inserted(returning)} VALUES (0, @user, @user, {values})";

    /// <inheritdoc />
    public override string UpdateReturning(string table, string set, string where, string returning, bool serialize = false)
        => $"UPDATE {Table(table)}{(serialize ? " WITH (UPDLOCK, HOLDLOCK)" : "")} SET {set} OUTPUT {Inserted(returning)} WHERE {where}";

    /// <inheritdoc />
    public override bool ReadBool(DbDataReader reader, int index) => reader.GetBoolean(index);

    /// <inheritdoc />
    public override DateTimeOffset ReadDateTime(DbDataReader reader, int index) => reader.GetFieldValue<DateTimeOffset>(index);

    /// <inheritdoc />
    public override DateOnly ReadDate(DbDataReader reader, int index) => DateOnly.FromDateTime(reader.GetDateTime(index));

    /// <inheritdoc />
    public override decimal ReadHours(DbDataReader reader, int index) => reader.GetDecimal(index);

    /// <inheritdoc />
    public override byte ReadByte(DbDataReader reader, int index) => reader.GetByte(index);

    /// <inheritdoc />
    public override DateOnly ToDate(object value) => DateOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture));

    /// <summary>OUTPUT の列の並び</summary>
    /// <remarks>列の並びの各列に、書いたあとの行を表す接頭辞を付ける。</remarks>
    /// <param name="columns">列の並び (表の別名なし)</param>
    /// <returns><c>OUTPUT</c> に書ける列の並び</returns>
    private static string Inserted(string columns)
        => string.Join(", ", columns.Split(',', StringSplitOptions.TrimEntries).Select(column => $"inserted.{column}"));
}
