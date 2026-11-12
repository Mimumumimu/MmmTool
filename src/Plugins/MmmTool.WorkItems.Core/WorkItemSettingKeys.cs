namespace MmmTool.WorkItems.Core;

/// <summary>作業管理がこの PC の設定に残す値のキー</summary>
/// <remarks>ホストがモードの切り替えで初期化するために使う</remarks>
public static class WorkItemSettingKeys
{
    /// <summary>表示しない案件の番号で、カンマでつないだ文字列</summary>
    /// <remarks>
    /// 例は 1,5,7。
    /// 無ければ、全部の案件を表示する。
    /// </remarks>
    public const string HiddenProjects = "WorkItems.HiddenProjects";

    /// <summary>自分の担当だけを表示するか</summary>
    /// <remarks>
    /// 真偽で、作業管理の表とスケジュールで共有する。
    /// 無ければ、オフ。
    /// </remarks>
    public const string MineOnly = "WorkItems.MineOnly";
}
