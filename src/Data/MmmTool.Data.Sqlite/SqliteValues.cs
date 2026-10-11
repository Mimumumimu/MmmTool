using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MmmTool.Data.Sqlite;

/// <summary>SQLite の値の読み書きの補助 (日時・日付・時間の文字や数への変換。コマンドの組み立て)</summary>
/// <remarks>
/// 日時は <c>2026-10-11T14:30:00.123+09:00</c> の形 (日本時間の時差つき・ミリ秒)で、いつも同じ形・同じ時差で書くので、文字の大小がそのまま日時の前後になる。
/// 日付は <c>2026-10-11</c>。時間は小数 2 桁に丸めた実数。
/// </remarks>
internal static class SqliteValues
{
    /// <summary>日時の書式</summary>
    private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffzzz";

    /// <summary>日付の書式</summary>
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>日本時間の時差</summary>
    private static readonly TimeSpan JapanOffset = TimeSpan.FromHours(9);

    /// <summary>日時を、保存する文字にする</summary>
    /// <param name="value">日時</param>
    /// <returns>日本時間の時差つき・ミリ秒までの文字</returns>
    public static string Text(DateTimeOffset value)
        => value.ToOffset(JapanOffset).ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>日付を、保存する文字にする</summary>
    /// <param name="value">日付</param>
    /// <returns><c>yyyy-MM-dd</c> の文字</returns>
    public static string Text(DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>保存した文字を、日時にする</summary>
    /// <param name="text">日時の文字</param>
    /// <returns>日時</returns>
    public static DateTimeOffset ParseDateTime(string text)
        => DateTimeOffset.ParseExact(text, DateTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>保存した文字を、日付にする</summary>
    /// <param name="text">日付の文字</param>
    /// <returns>日付</returns>
    public static DateOnly ParseDate(string text)
        => DateOnly.ParseExact(text, DateFormat, CultureInfo.InvariantCulture);

    /// <summary>保存した実数を、小数 2 桁の時間にする</summary>
    /// <param name="value">実数</param>
    /// <returns>小数 2 桁に丸めた時間</returns>
    public static decimal ToHours(double value) => Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);

    /// <summary>コマンドを作り、パラメーターを付ける</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL (値はパラメーターで渡す)</param>
    /// <param name="parameters">パラメーターの名前 (<c>@</c> つき)と値。日時・日付・小数・真偽は、保存する形に変える</param>
    /// <returns>コマンド。呼ぶ側が破棄する</returns>
    public static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, Convert(value));
        }
        return command;
    }

    /// <summary>パラメーターの値を、保存する形にする</summary>
    /// <param name="value">値</param>
    /// <returns>保存する形の値</returns>
    private static object Convert(object value) => value switch
    {
        DateTimeOffset dateTime => Text(dateTime),
        DateOnly date => Text(date),
        decimal number => (double)number,
        bool flag => flag ? 1L : 0L,
        byte small => (long)small,
        _ => value,
    };
}
