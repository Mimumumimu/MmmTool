using Microsoft.Data.Sqlite;
using MmmSdk.Db.Sqlite.Components.Connections;
using MmmTool.CodeMaster.Core;
using MmmTool.Data.CodeMaster;

namespace MmmTool.Data.Sqlite.CodeMaster;

/// <summary>
/// コードマスタの名前を、ローカルの SQLite (<c>Data/MmmTool.db</c>)で編集する。
/// </summary>
/// <param name="database">データベースファイルを開く入口</param>
/// <param name="time">更新日時を書くための時計 (SQLite にはサーバーの時計が無いので、アプリが書く)</param>
/// <remarks>
/// 名前の列だけを書き、更新日時と更新者を進める。更新日時が進むので、作業管理の差分の取得が、変えた行を拾う。
/// ユーザーは <c>Id = 1</c> の固定値。書き込みは、書き込みの許可を最初に取るトランザクションの中で行う。処理は別のスレッドで行い、SQL はパラメーター化だけで書く。
/// </remarks>
public sealed class SqliteCodeMasterRepository(SqliteDatabase database, TimeProvider time) : ICodeMasterRepository
{
    /// <summary>ローカルのユーザーの番号 (固定)</summary>
    private const int LocalUserId = 1;

    /// <inheritdoc />
    public bool CanEdit => true;

    /// <inheritdoc />
    public Task<IReadOnlyList<CodeMasterEntry>> LoadOptionsAsync(int featureNo, int categoryNo, CancellationToken cancellationToken = default)
        => Task.Run(
            () => database.RunAsync<IReadOnlyList<CodeMasterEntry>>(
                async connection =>
                {
                    await using var command = SqliteValues.Command(
                        connection,
                        null,
                        "SELECT c.Id, c.IsDeleted, c.CreatedAt, c.CreatedByUserId, c.UpdatedAt, c.UpdatedByUserId, c.FeatureNo, c.CategoryNo, c.CodeNo, c.SortOrder, c.NumericValue, c.Name FROM CodeMaster c WHERE c.FeatureNo = @feature AND c.CategoryNo = @category AND c.IsDeleted = 0 ORDER BY c.SortOrder, c.CodeNo",
                        [("@feature", featureNo), ("@category", categoryNo)]);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    var list = new List<CodeMasterEntry>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        list.Add(ReadCode(reader).ToEntry());
                    }
                    return list;
                },
                cancellationToken),
            cancellationToken);

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

        var now = time.GetUtcNow();
        return Task.Run(
            () => database.RunInTransactionAsync(
                async (connection, transaction) =>
                {
                    foreach (var (codeNo, name) in namesByCodeNo)
                    {
                        await using var command = SqliteValues.Command(
                            connection,
                            transaction,
                            "UPDATE CodeMaster SET Name = @name, UpdatedAt = @now, UpdatedByUserId = @user WHERE FeatureNo = @feature AND CategoryNo = @category AND CodeNo = @code AND IsDeleted = 0 AND Name <> @name",
                            [("@name", name), ("@now", now), ("@user", LocalUserId), ("@feature", featureNo), ("@category", categoryNo), ("@code", codeNo)]);
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                },
                cancellationToken),
            cancellationToken);
    }

    /// <summary>コードマスタの行を読む</summary>
    /// <param name="r">読み取り中の結果</param>
    /// <returns>行</returns>
    private static CodeMasterRow ReadCode(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetInt64(1) != 0,
        CreatedAt = SqliteValues.ParseDateTime(r.GetString(2)),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = SqliteValues.ParseDateTime(r.GetString(4)),
        UpdatedByUserId = r.GetInt32(5),
        FeatureNo = r.GetInt32(6),
        CategoryNo = r.GetInt32(7),
        CodeNo = r.GetInt32(8),
        SortOrder = r.GetInt32(9),
        NumericValue = r.GetInt32(10),
        Name = r.GetString(11),
    };
}
