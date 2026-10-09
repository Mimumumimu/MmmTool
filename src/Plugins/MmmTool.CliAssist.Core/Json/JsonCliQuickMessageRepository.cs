using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core.Json;

/// <summary>よく使う文を JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
public sealed class JsonCliQuickMessageRepository(IJsonFileStore store) : ICliQuickMessageRepository
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "CliQuickMessages.json";

    /// <inheritdoc />
    public async Task<DataLoadResult<List<CliQuickMessage>>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await store.ReadAsync(FileName, CliAssistJsonContext.Readable.ListCliQuickMessage, cancellationToken);
        // 無い・空・壊れていたときは、空の一覧にする (読み込みでは、ファイルは作らない。壊れていたときは退避済み)
        return new(result.Value ?? [], result.RecoveryMessage);
    }

    /// <inheritdoc />
    public Task SaveAsync(IReadOnlyList<CliQuickMessage> messages, CancellationToken cancellationToken = default)
        => store.WriteAsync(FileName, messages.ToList(), CliAssistJsonContext.Readable.ListCliQuickMessage, cancellationToken);
}
