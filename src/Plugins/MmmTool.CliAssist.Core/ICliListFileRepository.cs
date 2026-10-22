using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core;

/// <summary>編集画面で書くか、手で書く一覧のファイルの保存先</summary>
/// <typeparam name="TItem">一覧の項目の型</typeparam>
public interface ICliListFileRepository<TItem>
{
    /// <summary>読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>一覧と、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>保存先にまだ無ければ、ファイルは作らずに、空の一覧を返す。壊れていたときは退避して、空の一覧を返す。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<DataLoadResult<List<TItem>>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>一覧を保存する</summary>
    /// <param name="items">保存する一覧</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveAsync(IReadOnlyList<TItem> items, CancellationToken cancellationToken = default);
}
