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
}
