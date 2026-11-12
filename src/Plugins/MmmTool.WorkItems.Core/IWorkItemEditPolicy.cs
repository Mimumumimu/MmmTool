namespace MmmTool.WorkItems.Core;

/// <summary>更新してよいかの判定</summary>
/// <remarks>
/// 更新の入口の 1 か所に集める。
/// 将来、担当者と、リーダー的な人だけが更新できる実装を足す。
/// 誰でも更新できる実装 (<see cref="AnyoneCanEdit"/>)はそのあとも残す。
/// これはユーザーの決定。
/// </remarks>
public interface IWorkItemEditPolicy
{
    /// <summary>行を更新してよいか</summary>
    /// <param name="kind">行の種類</param>
    /// <param name="id">行の番号</param>
    /// <returns>更新してよければ true</returns>
    bool CanEdit(WorkRowKind kind, int id);
}
