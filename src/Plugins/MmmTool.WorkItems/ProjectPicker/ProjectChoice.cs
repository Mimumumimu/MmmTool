using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.ProjectPicker;

/// <summary>表示する案件を選ぶ一覧の 1 件 (名前・選んでいるか・完了済みか)</summary>
/// <param name="entry">案件マスターの 1 件</param>
public sealed partial class ProjectChoice(WorkProjectEntry entry) : ObservableObject
{
    /// <summary>案件の番号</summary>
    public int Id { get; } = entry.Id;

    /// <summary>名前</summary>
    public string Name { get; } = entry.Name;

    /// <summary>完了済みの印 (完了済みでなければ空)</summary>
    public string FinishedText { get; } = entry.IsFinished ? "完了" : "";

    /// <summary>選んでいるか</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; } = entry.IsSelected;
}
