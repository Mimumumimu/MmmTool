using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories.Json;

public sealed class JsonCliSettingsRepository(JsonFileStore store) : ICliSettingsRepository
{
    private const string FileName = "CliSettings.json";

    public async Task<CliSettings> LoadAsync(CancellationToken cancellationToken = default)
        => await store.ReadAsync(FileName, CoreJsonContext.Readable.CliSettings, cancellationToken) ?? new CliSettings();

    public Task SaveAsync(CliSettings settings, CancellationToken cancellationToken = default)
        => store.WriteAsync(FileName, settings, CoreJsonContext.Readable.CliSettings, cancellationToken);
}
