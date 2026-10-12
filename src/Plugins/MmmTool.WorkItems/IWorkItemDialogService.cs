namespace MmmTool.WorkItems;

/// <summary>作業管理のダイアログを開く (ViewModel から UI 型に触れずに使うための口)</summary>
public interface IWorkItemDialogService
{
    /// <summary>移動先のグループか案件を選ぶダイアログを開く</summary>
    /// <param name="destinations">選べる移動先 (選んでいる案件と、その中のグループ。木の順)</param>
    /// <returns>選ばれた移動先。キャンセルなら null</returns>
    Task<Move.MoveDestination?> PickDestinationAsync(IReadOnlyList<Move.MoveDestination> destinations);

    /// <summary>案件の管理のウィンドウを開き、閉じるまで待つ</summary>
    /// <returns>ウィンドウが閉じるまでの待機を表すタスク</returns>
    Task ShowProjectManagerAsync();

    /// <summary>作業報告のウィンドウを開き、閉じるまで待つ</summary>
    /// <returns>ウィンドウが閉じるまでの待機を表すタスク</returns>
    Task ShowReportAsync();
}
