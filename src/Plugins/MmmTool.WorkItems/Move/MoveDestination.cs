namespace MmmTool.WorkItems.Move;

/// <summary>移動先の選択肢 (選んでいる案件の直下、またはグループ 1 つ)</summary>
/// <param name="ProjectId">移動先の案件の番号</param>
/// <param name="GroupId">移動先のグループの番号 (0 は案件の直下)</param>
/// <param name="Name">表示する名前</param>
/// <param name="Level">字下げの段 (案件が 0)</param>
public sealed record MoveDestination(int ProjectId, int GroupId, string Name, int Level)
{
    /// <summary>字下げの余白 (一覧の項目の左に入れる)</summary>
    public Microsoft.UI.Xaml.Thickness Indent => new(Level * 20, 0, 0, 0);
}
