namespace MmmTool.WorkItems;

/// <summary>作業リストのダイアログを開く (ViewModel から UI 型に触れずに使うための口)</summary>
public interface IWorkItemDialogService
{
    /// <summary>移動先のグループを選ぶダイアログを開く</summary>
    /// <param name="destinations">選べる移動先 (最上位を含む。木の順)</param>
    /// <returns>選ばれた移動先の番号 (0 は最上位)。キャンセルなら null</returns>
    Task<int?> PickDestinationAsync(IReadOnlyList<Move.MoveDestination> destinations);

    /// <summary>進捗度のラベルを編集するダイアログを開く</summary>
    /// <param name="labels">今のラベル (値 → ラベル)</param>
    /// <returns>編集後のラベル (値 → ラベル。空はラベルなし)。キャンセルなら null</returns>
    Task<IReadOnlyDictionary<int, string>?> EditProgressLabelsAsync(IReadOnlyDictionary<int, string> labels);
}
