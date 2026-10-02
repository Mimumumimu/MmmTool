using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories;

/// <summary>リンクメニューの保存先</summary>
public interface ILinkRepository
{
    /// <summary>リンクメニューを読み込む</summary>
    /// <remarks>保存先にまだ無ければ空の構成を作って保存する。</remarks>
    /// <exception cref="DataFileException">読み込み・保存に失敗した。</exception>
    Task<LinkMenu> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>リンクメニューを保存する</summary>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveAsync(LinkMenu menu, CancellationToken cancellationToken = default);
}
