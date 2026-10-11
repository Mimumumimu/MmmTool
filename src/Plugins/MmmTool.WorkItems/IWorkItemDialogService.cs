namespace MmmTool.WorkItems;

/// <summary>作業リストのダイアログを開く (ViewModel から UI 型に触れずに使うための口)</summary>
public interface IWorkItemDialogService
{
    /// <summary>移動先のグループか案件を選ぶダイアログを開く</summary>
    /// <param name="destinations">選べる移動先 (選んでいる案件と、その中のグループ。木の順)</param>
    /// <returns>選ばれた移動先。キャンセルなら null</returns>
    Task<Move.MoveDestination?> PickDestinationAsync(IReadOnlyList<Move.MoveDestination> destinations);

    /// <summary>進捗度のラベルを編集するダイアログを開く</summary>
    /// <param name="labels">今のラベル (値 → ラベル)</param>
    /// <returns>編集後のラベル (値 → ラベル。空はラベルなし)。キャンセルなら null</returns>
    Task<IReadOnlyDictionary<int, string>?> EditProgressLabelsAsync(IReadOnlyDictionary<int, string> labels);

    /// <summary>案件の管理のウィンドウを開き、閉じるまで待つ</summary>
    /// <returns>ウィンドウが閉じるまでの待機を表すタスク</returns>
    Task ShowProjectManagerAsync();
}
