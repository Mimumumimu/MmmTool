using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.ProjectManager;

/// <summary>案件の管理の一覧の 1 件 (名前の入力と、進行中か完了済みか)</summary>
/// <param name="entry">案件マスターの 1 件</param>
public sealed partial class ProjectManagerItem(WorkProjectEntry entry) : ObservableObject
{
    /// <summary>新しい案件の入力中の行を作る (名前が入って確定するまで、保存しない)</summary>
    /// <returns>入力中の行</returns>
    public static ProjectManagerItem CreateNew() => new(new WorkProjectEntry(0, "", false, false, null)) { IsNew = true };

    /// <summary>入力中の新しい行 (番号が 0)か</summary>
    /// <param name="entry">案件マスターの 1 件</param>
    /// <returns>新しい行なら true</returns>
    private static bool IsNewInitial(WorkProjectEntry entry) => entry.Id == 0;

    /// <summary>まだ保存していない、入力中の行か</summary>
    public bool IsNew { get; private init; }

    /// <summary>案件の番号</summary>
    public int Id { get; } = entry.Id;

    /// <summary>保存されている名前 (入力が受け入れられなかったときに戻す先)</summary>
    public string SavedName { get; set; } = entry.Name;

    /// <summary>名前 (入力欄)</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = entry.Name;

    /// <summary>名前を入力中か (普段は文字だけを出し、クリックしたときだけ入力欄にする。入力中の新しい行は、最初から入力中)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLabel))]
    public partial bool IsEditing { get; set; } = IsNewInitial(entry);

    /// <summary>名前を文字だけで出しているか</summary>
    public bool IsLabel => !IsEditing;

    /// <summary>進行中か完了済みか (入力中の新しい行は空)</summary>
    public string StatusText => IsNew ? "" : entry.IsFinished ? "完了" : "進行中";
}
