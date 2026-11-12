using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.SqlServer.Connection;

namespace MmmTool.Data.SqlServer.CodeMaster;

/// <summary>コードマスタの名前を SQL Serverで編集する</summary>
/// <param name="database">SQL Server への入口</param>
/// <remarks>
/// 名前の列だけを書き、更新日時を DB の時計で進める。
/// 更新日時が進むので、作業管理の差分の取得が変えた行を拾う。
/// 更新者は 0 で、特定のユーザーではない。
/// この保存先を使う管理ツール MmmToolAdmin はユーザーを特定しないため。
/// MmmTool は DB モードでは、これを使わない。
/// DB モードのコードマスタは管理ツールだけが編集する。
/// 複数の名前は 1 文で書き、途中で失敗したら、すべて取り消す。
/// </remarks>
public sealed class SqlServerCodeMasterRepository(SqlServerDatabase database) : ICodeMasterRepository
{
    /// <inheritdoc />
    public bool CanEdit => true;

    /// <inheritdoc />
    public Task<IReadOnlyList<CodeMasterEntry>> LoadOptionsAsync(int featureNo, int categoryNo, CancellationToken cancellationToken = default)
        => database.RunAsync<IReadOnlyList<CodeMasterEntry>>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT {CodeMasterRowReader.Columns("c")}
                FROM dbo.CodeMaster c
                WHERE c.FeatureNo = @FeatureNo AND c.CategoryNo = @CategoryNo AND c.IsDeleted = 0
                ORDER BY c.SortOrder, c.CodeNo
                """;
            command.Parameters.Add(new SqlParameter("@FeatureNo", SqlDbType.Int) { Value = featureNo });
            command.Parameters.Add(new SqlParameter("@CategoryNo", SqlDbType.Int) { Value = categoryNo });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var list = new List<CodeMasterEntry>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(CodeMasterRowReader.Read(reader).ToEntry());
            }
            return list;
        }, cancellationToken);

    /// <inheritdoc />
    public Task SaveNamesAsync(int featureNo, int categoryNo, IReadOnlyDictionary<int, string> namesByCodeNo, CancellationToken cancellationToken = default)
    {
        foreach (var name in namesByCodeNo.Values)
        {
            if (name.Length > CodeMasterEntry.NameMaxLength)
            {
                throw new ArgumentException($"名前は {CodeMasterEntry.NameMaxLength} 文字以内で入力してください。", nameof(namesByCodeNo));
            }
        }

        if (namesByCodeNo.Count == 0)
        {
            return Task.CompletedTask;
        }

        // コードの数だけ流さず、コードと名前を JSON の 1 つのパラメーターで渡して、1 文で更新する。1 文なので、途中で失敗したら、すべて取り消される
        var names = JsonSerializer.Serialize(namesByCodeNo.Select(pair => new { code = pair.Key, name = pair.Value }));
        return database.RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE c SET c.Name = j.Name, c.UpdatedAt = SYSDATETIMEOFFSET(), c.UpdatedByUserId = 0
                FROM dbo.CodeMaster AS c
                INNER JOIN OPENJSON(@Names) WITH (CodeNo int '$.code', Name nvarchar(max) '$.name') AS j ON j.CodeNo = c.CodeNo
                WHERE c.FeatureNo = @FeatureNo AND c.CategoryNo = @CategoryNo AND c.IsDeleted = 0 AND c.Name <> j.Name
                """;
            command.Parameters.Add(new SqlParameter("@Names", SqlDbType.NVarChar, -1) { Value = names });
            command.Parameters.Add(new SqlParameter("@FeatureNo", SqlDbType.Int) { Value = featureNo });
            command.Parameters.Add(new SqlParameter("@CategoryNo", SqlDbType.Int) { Value = categoryNo });
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }
}
