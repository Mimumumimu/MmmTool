namespace MmmTool.WorkItems.Core;

/// <summary>作業管理の内容が変わった通知の、変わり方の種類</summary>
public enum WorkChangeKind
{
    /// <summary>どこが変わったかを限らない (受け取る側は、全体を作り直す)</summary>
    All,

    /// <summary>案件を、表示する案件に加えた (その案件の中身だけを読み足した)</summary>
    ProjectShown,

    /// <summary>案件を、表示する案件から外した (その案件の中身だけをメモリから外した)</summary>
    ProjectHidden,
}

/// <summary>作業管理の内容が変わった通知の内容</summary>
/// <param name="Kind">変わり方の種類</param>
/// <param name="ProjectId">加えた・外した案件の番号 (<see cref="WorkChangeKind.All"/> のときは 0)</param>
/// <remarks>
/// 受け取る側は、<see cref="WorkChangeKind.All"/> 以外の種類を、その案件の分だけ反映してよい。反映できないときは、全体を作り直せばよい (内容は、どちらでも同じになる)。
/// </remarks>
public sealed record WorkChangedEventArgs(WorkChangeKind Kind, int ProjectId = 0)
{
    /// <summary>どこが変わったかを限らない通知</summary>
    public static WorkChangedEventArgs All { get; } = new(WorkChangeKind.All);
}
