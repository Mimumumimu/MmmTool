using MmmSdk.Core.Storage;

namespace MmmTool.Core.Links.Json;

/// <summary>リンクメニューを JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
public sealed class JsonLinkRepository(IJsonFileStore store) : ILinkRepository
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

        var result = await store.ReadAsync(FileName, LinkJsonContext.Readable.LinkMenu, cancellationToken);
        if (result.RecoveryMessage is not null)
        {
            // 壊れたファイルは退避済みなので、無いときと同じく空の構成で作り直す
            return new(await WriteEmptyAsync(cancellationToken), result.RecoveryMessage);
        }
        var menu = result.Value ?? new LinkMenu();
        // JSON に null と書かれていたときも、空として扱う
        menu.Items ??= [];
        return new(menu);
    }

    /// <summary>空の構成を作って保存する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した空の構成</returns>
    /// <remarks>手で書き始めるときの雛形として、空の構成をファイルに出しておく。</remarks>
    private async Task<LinkMenu> WriteEmptyAsync(CancellationToken cancellationToken)
    {
        var empty = new LinkMenu();
        await store.WriteAsync(FileName, empty, LinkJsonContext.Readable.LinkMenu, cancellationToken);
        return empty;
    }

    /// <inheritdoc />
    public Task SaveAsync(LinkMenu menu, CancellationToken cancellationToken = default)
        => store.WriteAsync(FileName, menu, LinkJsonContext.Readable.LinkMenu, cancellationToken);
}
