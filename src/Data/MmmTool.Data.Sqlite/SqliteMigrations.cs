using MmmSdk.Db.Sqlite.Components.Connections;

namespace MmmTool.Data.Sqlite;

/// <summary>
/// データベースの版を上げるスクリプト (<c>Sql/NNN_*.sql</c>。埋め込みリソース)を読む。
/// </summary>
/// <remarks>ファイル名の先頭の数字が版になる (<c>001_CreateTables.sql</c> は版 1)。集計の作り直しのスクリプト (<c>Sql/Maintenance</c>)は、埋め込まない。</remarks>
internal static class SqliteMigrations
{
    /// <summary>リソース名の前置き (csproj の <c>LogicalName</c> と同じ)</summary>
    private const string ResourcePrefix = "MmmTool.Data.Sqlite.Sql.";

    /// <summary>あるべき形の埋め込みリソースの名前 (csproj の <c>LogicalName</c> と同じ)</summary>
    /// <remarks>番号つきのスクリプトの前置き (<see cref="ResourcePrefix"/>)とは別にする。同じ前置きだと、版を上げるスクリプトとして読まれる。</remarks>
    private const string SchemaResourceName = "MmmTool.Data.Sqlite.Schema.schema.sql";

    /// <summary>列の名前の変更 (リリース済みの DB が持つ古い名前を、<c>schema.sql</c> の名前に変える)</summary>
    /// <remarks>
    /// 古い名前の列がある DB だけ変わる。新しい DB は、<c>schema.sql</c> が新しい名前で作る。
    /// 名前を変える列が増えたら、ここに足す。次のリリース以降、古い名前の DB が無くなったら外す。
    /// </remarks>
    public static IReadOnlyList<SqliteColumnRename> ColumnRenames { get; } =
    [
        // 作業報告の予定から外す印。版 0.7.0 は ReportExcluded の名前で足した。印は予定だけを外すので、PlanExcluded にする
        new SqliteColumnRename("WorkItem", "ReportExcluded", "PlanExcluded"),
    ];

    /// <summary>埋め込んだスクリプトを、版の順に読む</summary>
    /// <returns>版を上げるスクリプト</returns>
    public static IReadOnlyList<SqliteMigration> Load()
    {
        var assembly = typeof(SqliteMigrations).Assembly;
        var migrations = new List<SqliteMigration>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var file = name[ResourcePrefix.Length..];
            var digits = new string([.. file.TakeWhile(char.IsAsciiDigit)]);
            if (!int.TryParse(digits, out var version))
            {
                throw new InvalidOperationException($"スクリプト {file} の名前が、番号で始まっていません。");
            }

            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"スクリプト {file} を読めません。");
            using var reader = new StreamReader(stream);
            migrations.Add(new SqliteMigration(version, reader.ReadToEnd()));
        }
        return migrations;
    }

    /// <summary>あるべき表と索引の SQL を読む</summary>
    /// <returns>あるべき形の SQL</returns>
    /// <remarks>埋め込みリソースの <c>Sql/schema.sql</c> を読む。番号つきのスクリプトとは別で、版の番号を持たない。列・索引・表を足す変更は、このファイルに書き足す。</remarks>
    public static string LoadSchema()
    {
        var assembly = typeof(SqliteMigrations).Assembly;
        using var stream = assembly.GetManifestResourceStream(SchemaResourceName) ?? throw new InvalidOperationException("あるべき形の schema.sql を読めません。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
