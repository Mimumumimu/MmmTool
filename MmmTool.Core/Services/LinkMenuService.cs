using MmmSdk.Core.Repositories;
using MmmTool.Core.Entities;
using MmmTool.Core.Repositories;

namespace MmmTool.Core.Services;

/// <summary>
/// リンクメニューを読み書きし、最後に読み込み・保存した構成を持つ。アプリ全体で 1 つ。
/// </summary>
/// <param name="repository">リンクメニューの保存先</param>
/// <remarks>トレイのリンクメニューは <see cref="Current"/> から作る（メニューを開くたびにファイルを読まないため）。</remarks>
public sealed class LinkMenuService(ILinkRepository repository)
{
    /// <summary>最後に読み込み・保存した構成</summary>
    /// <remarks>まだ読み込んでいなければ null。</remarks>
    public LinkMenu? Current { get; private set; }

    /// <summary>最後の読み込みに失敗したときのメッセージ</summary>
    /// <remarks>成功したら null に戻る。</remarks>
    public string? LoadError { get; private set; }

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ</summary>
    /// <remarks>起動時の読み込みで起きても編集ページで知らせられるよう、一度入ったら消さない。</remarks>
    public string? RecoveryMessage { get; private set; }

    /// <summary>リンクメニューを読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込んだリンクメニュー</returns>
    /// <exception cref="DataFileException">読み込みに失敗した（<see cref="LoadError"/> にも残す）。</exception>
    public async Task<LinkMenu> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (menu, recoveryMessage) = await repository.LoadAsync(cancellationToken);
            RecoveryMessage = recoveryMessage ?? RecoveryMessage;
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
    /// <param name="menu">保存するリンクメニュー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task SaveAsync(LinkMenu menu, CancellationToken cancellationToken = default)
    {
        await repository.SaveAsync(menu, cancellationToken);
        Current = menu;
        LoadError = null;
    }
}
