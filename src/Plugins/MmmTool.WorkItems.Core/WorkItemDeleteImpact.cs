namespace MmmTool.WorkItems.Core;

/// <summary>削除で消える件数 (確認ダイアログに出す)</summary>
/// <param name="Groups">消えるグループの数 (選んだ行がグループなら、その行も含む)</param>
/// <param name="Works">消える作業の数 (選んだ行が作業なら、その行も含む)</param>
/// <param name="Records">消える日ごとの記録の数</param>
public sealed record WorkItemDeleteImpact(int Groups, int Works, int Records);
