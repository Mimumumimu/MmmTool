namespace MmmTool.WorkItems.Core;

/// <summary>行の位置 (案件・親・兄弟の中の並び)。並べ替え・移動を、まとめて保存するために使う</summary>
/// <param name="Kind">行の種類 (グループまたは作業)</param>
/// <param name="Id">行の番号</param>
/// <param name="ProjectId">属する案件の番号</param>
/// <param name="ParentId">親のグループの番号 (0 は案件の直下)</param>
/// <param name="SortOrder">兄弟の中の並び</param>
public sealed record WorkPosition(WorkRowKind Kind, int Id, int ProjectId, int ParentId, int SortOrder);
