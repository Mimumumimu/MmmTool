using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories.Json;

public sealed class JsonLinkRepository(JsonFileStore store) : ILinkRepository
{
    private const string FileName = "Links.json";

    public async Task<LinkMenu> LoadAsync(CancellationToken cancellationToken = default)
    {
        var typeInfo = CoreJsonContext.Readable.LinkMenu;

        if (!store.Exists(FileName))
        {
            // 手で書き始めるときの雛形として、空の構成をファイルに出しておく
            var empty = new LinkMenu();
            await store.WriteAsync(FileName, empty, typeInfo, cancellationToken);
            return empty;
        }

        return await store.ReadAsync(FileName, typeInfo, cancellationToken) ?? new LinkMenu();
    }

    public Task SaveAsync(LinkMenu menu, CancellationToken cancellationToken = default)
        => store.WriteAsync(FileName, menu, CoreJsonContext.Readable.LinkMenu, cancellationToken);
}
