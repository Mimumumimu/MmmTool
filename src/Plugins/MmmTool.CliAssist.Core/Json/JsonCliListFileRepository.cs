using System.Text.Json.Serialization.Metadata;
using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core.Json;

/// <summary>一覧を JSON ファイルに保存する</summary>
/// <typeparam name="TItem">一覧の項目の型</typeparam>
/// <param name="store">JSON ファイルの読み書き</param>
/// <param name="fileName">保存先のファイル名</param>
/// <param name="typeInfo">一覧の JSON の読み書き (ソース生成)</param>
public abstract class JsonCliListFileRepository<TItem>(IJsonFileStore store, string fileName, JsonTypeInfo<List<TItem>> typeInfo)
    : ICliListFileRepository<TItem>
{
    /// <inheritdoc />
    public async Task<DataLoadResult<List<TItem>>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await store.ReadAsync(fileName, typeInfo, cancellationToken);
        // 無い・空・壊れていたときは、空の一覧にする。読み込みでは、ファイルを作らない。壊れていたときは退避済み
        return new(result.Value ?? [], result.RecoveryMessage);
    }

    /// <inheritdoc />
    public Task SaveAsync(IReadOnlyList<TItem> items, CancellationToken cancellationToken = default)
        => store.WriteAsync(fileName, items.ToList(), typeInfo, cancellationToken);
}
