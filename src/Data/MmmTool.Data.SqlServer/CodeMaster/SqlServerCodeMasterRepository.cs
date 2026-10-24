using System.Data;
using Microsoft.Data.SqlClient;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.SqlServer.Connection;

namespace MmmTool.Data.SqlServer.CodeMaster;

/// <summary>
/// コードマスタの名前を、SQL Server (<c>dbo.CodeMaster</c>)で編集する。
/// </summary>
/// <param name="database">SQL Server への入口</param>
/// <remarks>
/// 名前の列だけを書き、更新日時を DB の時計で進める。更新日時が進むので、作業管理の差分の取得が、変えた行を拾う。
/// 更新者は 0 で、特定のユーザーではない。この保存先を使う管理ツール MmmToolAdmin は、ユーザーを特定しないため。
/// MmmTool は、DB モードでは、これを使わない。DB モードのコードマスタは、管理ツールだけが編集する。
/// 複数の名前は、1 つのトランザクションで書き、途中で失敗したら、すべて取り消す。
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

        return database.RunAsync(async connection =>
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            foreach (var (codeNo, name) in namesByCodeNo)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    UPDATE dbo.CodeMaster
                    SET Name = @Name, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = 0
                    WHERE FeatureNo = @FeatureNo AND CategoryNo = @CategoryNo AND CodeNo = @CodeNo AND IsDeleted = 0 AND Name <> @Name
                    """;
                command.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, CodeMasterEntry.NameMaxLength) { Value = name });
                command.Parameters.Add(new SqlParameter("@FeatureNo", SqlDbType.Int) { Value = featureNo });
                command.Parameters.Add(new SqlParameter("@CategoryNo", SqlDbType.Int) { Value = categoryNo });
                command.Parameters.Add(new SqlParameter("@CodeNo", SqlDbType.Int) { Value = codeNo });
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }
}
