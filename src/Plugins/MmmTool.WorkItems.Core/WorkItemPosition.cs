namespace MmmTool.WorkItems.Core;

/// <summary>行の位置 (親と兄弟の中の並び)。並べ替え・移動を、まとめて保存するために使う</summary>
/// <param name="Id">行の番号</param>
/// <param name="ParentId">親の番号 (0 は最上位)</param>
/// <param name="SortOrder">兄弟の中の並び</param>
public sealed record WorkItemPosition(int Id, int ParentId, int SortOrder);
