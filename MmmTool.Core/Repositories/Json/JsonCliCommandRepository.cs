using MmmTool.Core.Entities;
using MmmTool.Core.Services;

namespace MmmTool.Core.Repositories.Json;

public sealed class JsonCliCommandRepository(JsonFileStore store) : ICliCommandRepository
{
    private const string FileName = "CliCommands.json";

    public async Task<CliCommandSet> LoadAsync(CancellationToken cancellationToken = default)
    {
        var typeInfo = CoreJsonContext.Readable.CliCommandSet;

        if (!store.Exists(FileName))
        {
            var defaults = CliCommandDefaults.Create();
            await store.WriteAsync(FileName, defaults, typeInfo, cancellationToken);
            return defaults;
        }

        // 中身が null（手修正で空にした等）でも既定で上書きはせず、空のまま扱う
        return await store.ReadAsync(FileName, typeInfo, cancellationToken) ?? new CliCommandSet();
    }
}
