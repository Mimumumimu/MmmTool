using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core;

/// <summary>よく使う文の保存先</summary>
public interface ICliQuickMessageRepository
{
    /// <summary>読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>よく使う文と、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>保存先にまだ無ければ、空の一覧を返す (ファイルは作らない)。壊れていたときは退避して、空の一覧を返す。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<DataLoadResult<List<CliQuickMessage>>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>よく使う文を保存する</summary>
    /// <param name="messages">保存するよく使う文</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveAsync(IReadOnlyList<CliQuickMessage> messages, CancellationToken cancellationToken = default);
}
