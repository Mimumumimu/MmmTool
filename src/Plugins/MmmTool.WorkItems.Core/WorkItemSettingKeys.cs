namespace MmmTool.WorkItems.Core;

/// <summary>作業管理が、この PC の設定に残す値のキー (ホストが、モードの切り替えで初期化するために使う)</summary>
public static class WorkItemSettingKeys
{
    /// <summary>表示しない案件の番号 (カンマでつないだ文字列。例: <c>1,5,7</c>。無ければ、全部の案件を表示する)</summary>
    public const string HiddenProjects = "WorkItems.HiddenProjects";
}
