namespace MmmTool.WorkItems.Core;

/// <summary>誰でも更新できる</summary>
public sealed class AnyoneCanEdit : IWorkItemEditPolicy
{
    /// <inheritdoc />
    public bool CanEdit(WorkRowKind kind, int id) => true;
}
