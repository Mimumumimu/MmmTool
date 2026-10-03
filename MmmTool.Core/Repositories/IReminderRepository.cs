using MmmSdk.Core.Repositories;
using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories;

/// <summary>リマインダー（本体・対応状態）の保存先</summary>
public interface IReminderRepository
{
    /// <summary>リマインダー本体の一覧を読み込む（論理削除済みも含む）</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>リマインダー本体の一覧と、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>保存先にまだ無い・空なら空の一覧。壊れていたときは退避してから空の一覧として扱う。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<DataLoadResult<List<Reminder>>> LoadRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>リマインダー本体の一覧を保存する</summary>
    /// <param name="reminders">保存するリマインダー本体の一覧</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveRemindersAsync(IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default);

    /// <summary>対応状態の一覧を読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対応状態の一覧と、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>保存先にまだ無い・空なら空の一覧。壊れていたときは退避してから空の一覧として扱う。</remarks>
    /// <exception cref="DataFileException">読み込みに失敗した。</exception>
    Task<DataLoadResult<List<ReminderState>>> LoadStatesAsync(CancellationToken cancellationToken = default);

    /// <summary>対応状態の一覧を保存する</summary>
    /// <param name="states">保存する対応状態の一覧</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveStatesAsync(IReadOnlyList<ReminderState> states, CancellationToken cancellationToken = default);
}
