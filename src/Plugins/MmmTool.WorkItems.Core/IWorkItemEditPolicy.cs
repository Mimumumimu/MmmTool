namespace MmmTool.WorkItems.Core;

/// <summary>更新してよいかの判定 (更新の入口の 1 か所に集める)</summary>
/// <remarks>将来、担当者と、リーダー的な人だけが更新できる実装を足す。誰でも更新できる実装 (<see cref="AnyoneCanEdit"/>)は、そのあとも残す (ユーザーの決定)。</remarks>
public interface IWorkItemEditPolicy
{
    /// <summary>行を更新してよいか</summary>
    /// <param name="kind">行の種類</param>
    /// <param name="id">行の番号</param>
    /// <returns>更新してよければ true</returns>
    bool CanEdit(WorkRowKind kind, int id);
}
