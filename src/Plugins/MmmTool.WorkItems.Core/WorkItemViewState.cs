using MmmSdk.Core.Utilities;

namespace MmmTool.WorkItems.Core;

/// <summary>
/// 作業管理の表で、画面で選んだ状態。ページを作り直しても引き継ぐために、ページの外で持つ。
/// </summary>
/// <param name="displaySettings">起動時の表示の設定</param>
/// <remarks>
/// ページは、別のウィンドウに外す・戻す・機能をオフにしてオンに戻すときに作り直される。そのとき ViewModel は、作るときにここから読み、画面で変えるたびにここへ書く。
/// アプリが動いている間だけ持ち、ファイルには保存しない。起動したときの値は、設定の初期値 (<see cref="WorkItemDisplaySettings"/>)で決める。
/// 保存していない編集と処理中の表示は持たない。引き継ぐと、古い状態が残って紛らわしいため。
/// </remarks>
public sealed class WorkItemViewState(WorkItemDisplaySettings displaySettings)
{
    /// <summary>入力日 (同じ日の間だけ引き継ぐ)</summary>
    public RememberedDate InputDate { get; } = new();

    /// <summary>「過去の完了」の選択</summary>
    public CompletedDisplay Completed { get; set; } = displaySettings.PastCompleted;

    /// <summary>絞り込みで隠す状態のコード番号 (空なら、すべての状態を出す)</summary>
    public HashSet<int> HiddenStatuses { get; } = [];

    /// <summary>絞り込みで、開始日がまだ来ていない作業を隠すか</summary>
    public bool HideNotStarted { get; set; }

    /// <summary>利用者が閉じた案件・グループ</summary>
    public HashSet<WorkKey> Collapsed { get; } = [];

    /// <summary>利用者が開いた案件・グループ</summary>
    public HashSet<WorkKey> Expanded { get; } = [];

    /// <summary>選んでいる行の識別 (選んでいなければ null)</summary>
    public WorkKey? SelectedKey { get; set; }
}
