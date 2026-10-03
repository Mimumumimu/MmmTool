using MmmSdk.Core.Storage;

namespace MmmTool.Core.CliAssist.Json;

/// <summary>CLI補助の利用状態を JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
public sealed class JsonCliSettingsRepository(IJsonFileStore store) : ICliSettingsRepository
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "CliSettings.json";

    /// <inheritdoc />
    public async Task<DataLoadResult<CliSettings>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await store.ReadAsync(FileName, CliAssistJsonContext.Readable.CliSettings, cancellationToken);
        // 壊れていたときも、無いときと同じく空の設定から始める（ファイルは次の保存で作られる）
        return new(result.Value ?? new CliSettings(), result.RecoveryMessage);
    }

    /// <inheritdoc />
    public Task SaveAsync(CliSettings settings, CancellationToken cancellationToken = default)
        => store.WriteAsync(FileName, settings, CliAssistJsonContext.Readable.CliSettings, cancellationToken);
}
