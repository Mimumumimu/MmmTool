using MmmSdk.Core.Components.Storage;

namespace MmmTool.Core.Links;

/// <summary>リンクメニューの保存先</summary>
public interface ILinkRepository
{
    /// <summary>リンクメニューを読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>リンクメニューと、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>保存先にまだ無ければ空の構成を作って保存する。壊れていたときは退避してから空の構成で作り直す。</remarks>
    /// <exception cref="DataFileException">読み込み・保存に失敗した。</exception>
    Task<DataLoadResult<LinkMenu>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>リンクメニューを保存する</summary>
    /// <param name="menu">保存するリンクメニュー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveAsync(LinkMenu menu, CancellationToken cancellationToken = default);
}
