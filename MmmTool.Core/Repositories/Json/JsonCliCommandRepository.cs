using MmmSdk.Core.Repositories;
using MmmSdk.Core.Repositories.Json;
using MmmTool.Core.Entities;
using MmmTool.Core.Services;

namespace MmmTool.Core.Repositories.Json;

/// <summary>定型コマンドを JSON ファイルに保存する</summary>
public sealed class JsonCliCommandRepository(JsonFileStore store) : ICliCommandRepository
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "CliCommands.json";

    /// <inheritdoc />
    public async Task<DataLoadResult<CliCommandSet>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var typeInfo = CoreJsonContext.Readable.CliCommandSet;

        if (!store.Exists(FileName))
        {
            return new(await WriteDefaultsAsync(cancellationToken));
        }

        var result = await store.ReadAsync(FileName, typeInfo, cancellationToken);
        if (result.RecoveryMessage is not null)
        {
            // 壊れたファイルは退避済みなので、無いときと同じく既定で作り直す
            return new(await WriteDefaultsAsync(cancellationToken), result.RecoveryMessage);
        }

        // 中身が null（手修正で空にした等）でも既定で上書きはせず、空のまま扱う
        return new(result.Value ?? new CliCommandSet());
    }

    /// <summary>既定の定型コマンドを作って保存する</summary>
    private async Task<CliCommandSet> WriteDefaultsAsync(CancellationToken cancellationToken)
    {
        var defaults = CliCommandDefaults.Create();
        await store.WriteAsync(FileName, defaults, CoreJsonContext.Readable.CliCommandSet, cancellationToken);
        return defaults;
    }
}
