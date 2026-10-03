using MmmSdk.Core.Repositories;
using MmmSdk.Core.Repositories.Json;
using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories.Json;

/// <summary>リンクメニューを JSON ファイルに保存する</summary>
public sealed class JsonLinkRepository(JsonFileStore store) : ILinkRepository
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "Links.json";

    /// <inheritdoc />
    public async Task<DataLoadResult<LinkMenu>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!store.Exists(FileName))
        {
            return new(await WriteEmptyAsync(cancellationToken));
        }

        var result = await store.ReadAsync(FileName, CoreJsonContext.Readable.LinkMenu, cancellationToken);
        if (result.RecoveryMessage is not null)
        {
            // 壊れたファイルは退避済みなので、無いときと同じく空の構成で作り直す
            return new(await WriteEmptyAsync(cancellationToken), result.RecoveryMessage);
        }
        return new(result.Value ?? new LinkMenu());
    }

    /// <summary>空の構成を作って保存する</summary>
    /// <remarks>手で書き始めるときの雛形として、空の構成をファイルに出しておく。</remarks>
    private async Task<LinkMenu> WriteEmptyAsync(CancellationToken cancellationToken)
    {
        var empty = new LinkMenu();
        await store.WriteAsync(FileName, empty, CoreJsonContext.Readable.LinkMenu, cancellationToken);
        return empty;
    }

    /// <inheritdoc />
    public Task SaveAsync(LinkMenu menu, CancellationToken cancellationToken = default)
        => store.WriteAsync(FileName, menu, CoreJsonContext.Readable.LinkMenu, cancellationToken);
}
