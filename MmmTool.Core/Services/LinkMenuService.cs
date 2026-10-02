using MmmTool.Core.Entities;
using MmmTool.Core.Repositories;

namespace MmmTool.Core.Services;

/// <summary>
/// リンクメニューを読み書きし、最後に読み込み・保存した構成を持つ。アプリ全体で 1 つ。
/// </summary>
/// <remarks>トレイのリンクメニューは <see cref="Current"/> から作る（メニューを開くたびにファイルを読まないため）。</remarks>
public sealed class LinkMenuService(ILinkRepository repository)
{
    /// <summary>最後に読み込み・保存した構成</summary>
    /// <remarks>まだ読み込んでいなければ null。</remarks>
    public LinkMenu? Current { get; private set; }

    /// <summary>最後の読み込みに失敗したときのメッセージ</summary>
    /// <remarks>成功したら null に戻る。</remarks>
    public string? LoadError { get; private set; }

    /// <summary>リンクメニューを読み込む</summary>
    /// <exception cref="DataFileException">読み込みに失敗した（<see cref="LoadError"/> にも残す）。</exception>
    public async Task<LinkMenu> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var menu = await repository.LoadAsync(cancellationToken);
            menu.Items ??= [];
            Current = menu;
            LoadError = null;
            return menu;
        }
        catch (DataFileException ex)
        {
            LoadError = ex.Message;
            throw;
        }
    }

    /// <summary>リンクメニューを保存する</summary>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task SaveAsync(LinkMenu menu, CancellationToken cancellationToken = default)
    {
        await repository.SaveAsync(menu, cancellationToken);
        Current = menu;
        LoadError = null;
    }
}
