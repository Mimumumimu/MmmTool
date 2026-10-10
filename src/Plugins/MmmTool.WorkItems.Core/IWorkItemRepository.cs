using MmmSdk.Core.Components.Storage;

namespace MmmTool.WorkItems.Core;

/// <summary>作業リストの保存先</summary>
/// <remarks>
/// 1 件単位の操作で書く。番号 (<see cref="WorkItem.Id"/>)は保存先が決める。見えるのも書けるのも、自分の分だけ。
/// 削除は論理削除だけ (完全削除はしない)。失敗は <see cref="DataFileException"/>。
/// </remarks>
public interface IWorkItemRepository
{
    /// <summary>進捗度のラベルを、画面から編集できるか</summary>
    /// <remarks>ローカルの保存先は true。全員に影響する保存先は false にし、直接直す。</remarks>
    bool CanEditProgressLabels { get; }

    /// <summary>全データを読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>全データと、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>保存先にまだ無いものは、空 (進捗度のラベルは初期値)で作る。壊れていたファイルは退避してから空で作り直す。</remarks>
    /// <exception cref="DataFileException">読み込み・保存に失敗した。</exception>
    Task<DataLoadResult<WorkItemSnapshot>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>行を追加する</summary>
    /// <param name="item">追加する行 (<see cref="WorkItem.Id"/> は無視する)</param>
    /// <param name="siblingPositions">同時に直す、兄弟の位置 (無ければ空)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>番号が決まった、保存した行</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task<WorkItem> AddAsync(WorkItem item, IReadOnlyList<WorkItemPosition> siblingPositions, CancellationToken cancellationToken = default);

    /// <summary>行を更新する</summary>
    /// <param name="item">更新後の行</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task UpdateAsync(WorkItem item, CancellationToken cancellationToken = default);

    /// <summary>行の位置 (親・並び)を、まとめて更新する</summary>
    /// <param name="positions">更新する位置</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task UpdatePositionsAsync(IReadOnlyList<WorkItemPosition> positions, CancellationToken cancellationToken = default);

    /// <summary>行を論理削除する</summary>
    /// <param name="ids">削除する行の番号 (グループなら、中身の分もすべて渡す)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除の完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SetDeletedAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default);

    /// <summary>日ごとの記録を保存する</summary>
    /// <param name="record">保存する記録</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>同じ作業・日付があれば上書きする。実績も備考も空 (<see cref="WorkRecord.IsEmpty"/>)なら、記録を消す。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SetRecordAsync(WorkRecord record, CancellationToken cancellationToken = default);

    /// <summary>進捗度のラベルを保存する</summary>
    /// <param name="labels">ラベル (値ごと。ラベルが空の値は、ラベルなし)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="InvalidOperationException"><see cref="CanEditProgressLabels"/> が false の保存先。</exception>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    Task SaveProgressLabelsAsync(IReadOnlyList<WorkProgressLabel> labels, CancellationToken cancellationToken = default);
}
