using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories.Json;

/// <summary>CLI補助の利用状態を JSON ファイルに保存する</summary>
public sealed class JsonCliSettingsRepository(JsonFileStore store) : ICliSettingsRepository
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "CliSettings.json";

    /// <inheritdoc />
    public async Task<CliSettings> LoadAsync(CancellationToken cancellationToken = default)
        => await store.ReadAsync(FileName, CoreJsonContext.Readable.CliSettings, cancellationToken) ?? new CliSettings();

    /// <inheritdoc />
    public Task SaveAsync(CliSettings settings, CancellationToken cancellationToken = default)
        => store.WriteAsync(FileName, settings, CoreJsonContext.Readable.CliSettings, cancellationToken);
}
